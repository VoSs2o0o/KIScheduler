using KIScheduler.Core.Contracts;
using KIScheduler.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace KIScheduler.Infrastructure.Persistence;

/// <summary>
/// Repairs state left behind by a process that stopped without completing its shutdown path.
/// All changes are committed together so that recovery itself cannot leave a half-repaired job.
/// </summary>
public sealed class SqliteStartupRecoveryRepository(
    IDbContextFactory<KischedulerDbContext> contextFactory) : IStartupRecoveryRepository
{
    public async Task<StartupRecoveryResult> RecoverInterruptedAsync(DateTimeOffset detectedAtUtc,
        string recoveryOwnerId, CancellationToken cancellationToken = default)
    {
        if (detectedAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Der Erkennungszeitpunkt muss in UTC vorliegen.", nameof(detectedAtUtc));
        ArgumentException.ThrowIfNullOrWhiteSpace(recoveryOwnerId);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var interrupted = await db.WorkItems
            .Where(x => x.Status == (int)WorkItemStatus.Reserviert
                || x.Status == (int)WorkItemStatus.InBearbeitung)
            .ToListAsync(cancellationToken);
        interrupted = interrupted.OrderBy(x => x.CreatedAtUtc).ToList();
        var createdHolds = 0;

        foreach (var row in interrupted)
        {
            var previousStatus = (WorkItemStatus)row.Status;
            var latestPersistedAttempt = await db.ExecutionAttempts.AsNoTracking()
                .Where(x => x.WorkItemId == row.Id)
                .OrderByDescending(x => x.SequenceNumber)
                .FirstOrDefaultAsync(cancellationToken);
            ExecutionAttemptRow? recoveryAttempt = null;
            if (previousStatus == WorkItemStatus.InBearbeitung)
            {
                var sequence = (await db.ExecutionAttempts
                    .Where(x => x.WorkItemId == row.Id)
                    .MaxAsync(x => (int?)x.SequenceNumber, cancellationToken) ?? 0) + 1;
                var startedAt = row.FirstAttemptStartedAtUtc is { } first && first <= detectedAtUtc
                    ? first : detectedAtUtc;
                recoveryAttempt = new ExecutionAttemptRow
                {
                    Id = Guid.NewGuid(),
                    WorkItemId = row.Id,
                    SequenceNumber = sequence,
                    PlatformId = row.PlatformId,
                    PlatformProfileId = row.PlatformProfileId,
                    ModelId = row.ModelId,
                    Effort = row.Effort,
                    StartedAtUtc = startedAt,
                    CompletedAtUtc = detectedAtUtc,
                    Result = (int)ExecutionAttemptResult.Unterbrochen,
                    SessionId = latestPersistedAttempt?.SessionId,
                    Diagnostic = "Die Anwendung wurde beendet, bevor ein Ausführungsergebnis gespeichert werden konnte."
                };
                db.ExecutionAttempts.Add(recoveryAttempt);

                if (row.ProjectId.HasValue && !await db.ProjectExecutionHolds.AnyAsync(x =>
                    x.ProjectId == row.ProjectId.Value && x.ReleasedAtUtc == null, cancellationToken))
                {
                    db.ProjectExecutionHolds.Add(new ProjectExecutionHoldRow
                    {
                        Id = Guid.NewGuid(),
                        ProjectId = row.ProjectId.Value,
                        TriggeringWorkItemId = row.Id,
                        PlatformId = row.PlatformId,
                        Reason = "App-Abbruch während der Ausführung; der Projektarbeitsbaum kann teilweise verändert sein.",
                        CreatedAtUtc = detectedAtUtc,
                        ReleaseRule = (int)ProjectHoldReleaseRule.ErfolgreicherAbschlussOderExpliziterAbbruch
                    });
                    createdHolds++;
                }
            }

            var latestAttempt = latestPersistedAttempt;
            var eventRows = await db.ExecutionEvents.AsNoTracking()
                .Where(x => x.WorkItemId == row.Id)
                .ToListAsync(cancellationToken);
            var latestEvent = eventRows.MaxBy(x => x.OccurredAtUtc);
            var data = new Dictionary<string, string>
            {
                ["reasonCode"] = "recovery.orphaned_execution",
                ["previousStatus"] = previousStatus.ToString(),
                ["platformId"] = row.PlatformId,
                ["platformProfileId"] = row.PlatformProfileId.ToString("D"),
                ["modelId"] = row.ModelId,
                ["projectId"] = row.ProjectId?.ToString() ?? "",
                ["recoveryOwnerId"] = recoveryOwnerId.Trim(),
                ["logReference"] = $"execution-events:{row.Id}"
            };
            if (!string.IsNullOrWhiteSpace(latestAttempt?.SessionId))
                data["lastSessionId"] = latestAttempt.SessionId;
            if (latestAttempt is not null) data["lastAttemptId"] = latestAttempt.Id.ToString();
            if (latestEvent is not null) data["lastEventId"] = latestEvent.Id.ToString();

            db.ExecutionEvents.Add(PersistenceMappings.ToRow(new ExecutionEvent(
                Guid.NewGuid(), new WorkItemId(row.Id), new PlatformProfileId(row.PlatformProfileId),
                detectedAtUtc, ExecutionEventSeverity.Error,
                "recovery.interrupted",
                $"Verwaister Zustand '{previousStatus}' wurde beim Start erkannt und sicher auf 'Unterbrochen' gesetzt.",
                recoveryAttempt is null ? null : new ExecutionAttemptId(recoveryAttempt.Id), data)));
            row.Status = (int)WorkItemStatus.Unterbrochen;
        }

        var removedLeases = await db.SchedulerLeases.ExecuteDeleteAsync(cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new StartupRecoveryResult(interrupted.Count, createdHolds, removedLeases);
    }
}
