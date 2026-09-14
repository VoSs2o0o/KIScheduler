using KIScheduler.Core.Contracts;
using KIScheduler.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace KIScheduler.Infrastructure.Persistence;

public sealed class SqliteUsageSnapshotRepository(IDbContextFactory<KischedulerDbContext> contextFactory) : IUsageSnapshotRepository
{
    public async Task<UsageSnapshot?> GetLatestAsync(PlatformProfileId platformProfileId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var snapshots = await db.UsageSnapshots.AsNoTracking()
            .Where(x => x.PlatformProfileId == platformProfileId.Value).ToListAsync(cancellationToken);
        var row = snapshots.MaxBy(x => x.ReadAtUtc);
        if (row is null) return null;
        var windows = await db.UsageWindows.AsNoTracking().Where(x => x.SnapshotId == row.Id).ToListAsync(cancellationToken);
        return new UsageSnapshot(new(row.PlatformId), new(row.PlatformProfileId), row.ReadAtUtc,
            row.Source, (UsageQuality)row.Quality,
            windows.Select(x => new UsageWindow(x.Name, new(x.UsedPercent), x.ResetAtUtc, x.Source, x.ReadAtUtc,
                (UsageQuality)x.Quality, x.RateLimitReachedType,
                x.WindowDurationTicks.HasValue ? TimeSpan.FromTicks(x.WindowDurationTicks.Value) : null,
                x.LimitId, x.LimitName)));
    }

    public async Task SaveAsync(UsageSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var id = Guid.NewGuid();
        db.UsageSnapshots.Add(new UsageSnapshotRow
        {
            Id = id,
            PlatformId = snapshot.PlatformId.Value,
            PlatformProfileId = snapshot.PlatformProfileId.Value,
            ReadAtUtc = snapshot.ReadAtUtc,
            Source = snapshot.Source,
            Quality = (int)snapshot.Quality
        });
        db.UsageWindows.AddRange(snapshot.Windows.Select(x => new UsageWindowRow
        {
            Id = Guid.NewGuid(),
            SnapshotId = id,
            Name = x.Name,
            LimitId = x.LimitId,
            LimitName = x.LimitName,
            UsedPercent = x.UsedPercent.Value,
            ResetAtUtc = x.ResetAtUtc,
            Source = x.Source,
            ReadAtUtc = x.ReadAtUtc,
            Quality = (int)x.Quality,
            RateLimitReachedType = x.RateLimitReachedType,
            WindowDurationTicks = x.WindowDuration?.Ticks
        }));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task InvalidateAsync(PlatformProfileId platformProfileId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var ids = await db.UsageSnapshots.Where(x => x.PlatformProfileId == platformProfileId.Value)
            .Select(x => x.Id).ToListAsync(cancellationToken);
        await db.UsageSnapshots.Where(x => ids.Contains(x.Id)).ExecuteDeleteAsync(cancellationToken);
    }
}

public sealed class SqliteUsagePolicyRepository(IDbContextFactory<KischedulerDbContext> contextFactory) : IUsagePolicyRepository
{
    public async Task<IReadOnlyList<UsagePolicy>> ListAsync(PlatformId? platformId = null,
        CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var query = db.UsagePolicies.AsNoTracking();
        if (platformId is not null) query = query.Where(x => x.PlatformId == platformId.Value);
        var rows = await query.OrderBy(x => x.PlatformId).ThenBy(x => x.LocalStartTicks).ToListAsync(cancellationToken);
        return rows.Select(ToDomain).ToList();
    }

    public async Task ReplaceAsync(IReadOnlyCollection<UsagePolicy> policies, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.UsagePolicies.ExecuteDeleteAsync(cancellationToken);
        db.UsagePolicies.AddRange(policies.Select(ToRow));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static UsagePolicyRow ToRow(UsagePolicy value) => new()
    {
        Id = Guid.NewGuid(),
        PlatformId = value.PlatformId.Value,
        ModelId = value.ModelId?.Value,
        DaysMask = value.Days.Aggregate(0, (mask, day) => mask | 1 << (int)day),
        LocalStartTicks = value.LocalStart.Ticks,
        LocalEndTicks = value.LocalEnd.Ticks,
        TimeZoneId = value.TimeZoneId,
        MaxUsedPercent = value.MaxUsedPercent.Value,
        UnknownUsageBehavior = (int)value.UnknownUsageBehavior,
        RefreshIntervalTicks = value.RefreshInterval.Ticks,
        EndSprintDurationTicks = value.EndSprintDuration?.Ticks,
        EndSprintMaxUsedPercent = value.EndSprintMaxUsedPercent?.Value
    };

    private static UsagePolicy ToDomain(UsagePolicyRow value) => new(new(value.PlatformId),
        Enum.GetValues<DayOfWeek>().Where(day => (value.DaysMask & 1 << (int)day) != 0),
        new TimeOnly(value.LocalStartTicks), new TimeOnly(value.LocalEndTicks), value.TimeZoneId,
        new(value.MaxUsedPercent), (UnknownUsageBehavior)value.UnknownUsageBehavior,
        TimeSpan.FromTicks(value.RefreshIntervalTicks), value.ModelId is null ? null : new(value.ModelId),
        value.EndSprintDurationTicks.HasValue ? TimeSpan.FromTicks(value.EndSprintDurationTicks.Value) : null,
        value.EndSprintMaxUsedPercent.HasValue ? new UsagePercent(value.EndSprintMaxUsedPercent.Value) : null);
}

public sealed class SqliteExecutionHistoryRepository(IDbContextFactory<KischedulerDbContext> contextFactory) : IExecutionHistoryRepository
{
    public async Task AddAttemptAsync(ExecutionAttempt attempt, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        db.ExecutionAttempts.Add(PersistenceMappings.ToRow(attempt)); await db.SaveChangesAsync(cancellationToken);
    }
    public async Task AddEventAsync(ExecutionEvent executionEvent, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        db.ExecutionEvents.Add(PersistenceMappings.ToRow(executionEvent)); await db.SaveChangesAsync(cancellationToken);
    }
    public async Task<IReadOnlyList<ExecutionAttempt>> ListAttemptsAsync(WorkItemId workItemId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return (await db.ExecutionAttempts.AsNoTracking().Where(x => x.WorkItemId == workItemId.Value)
            .OrderBy(x => x.SequenceNumber).ToListAsync(cancellationToken)).Select(PersistenceMappings.ToDomain).ToList();
    }
    public async Task<IReadOnlyList<ExecutionEvent>> ListEventsAsync(WorkItemId workItemId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return (await db.ExecutionEvents.AsNoTracking().Where(x => x.WorkItemId == workItemId.Value)
            .ToListAsync(cancellationToken)).OrderBy(x => x.OccurredAtUtc).Select(PersistenceMappings.ToDomain).ToList();
    }
}

public sealed class SqliteExecutionBlockRepository(IDbContextFactory<KischedulerDbContext> contextFactory) : IExecutionBlockRepository
{
    public async Task<IReadOnlyList<PlatformUsageBlock>> ListActivePlatformBlocksAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return (await db.PlatformUsageBlocks.AsNoTracking().Where(x => x.ReleasedAtUtc == null)
            .ToListAsync(cancellationToken)).OrderBy(x => x.CreatedAtUtc).Select(PersistenceMappings.ToDomain).ToList();
    }
    public async Task<IReadOnlyList<ProjectExecutionHold>> ListActiveProjectHoldsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return (await db.ProjectExecutionHolds.AsNoTracking().Where(x => x.ReleasedAtUtc == null)
            .ToListAsync(cancellationToken)).OrderBy(x => x.CreatedAtUtc).Select(PersistenceMappings.ToDomain).ToList();
    }
    public async Task SaveAsync(PlatformUsageBlock block, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var row = PersistenceMappings.ToRow(block); var existing = await db.PlatformUsageBlocks.SingleOrDefaultAsync(x => x.Id == row.Id, cancellationToken);
        if (existing is null) db.Add(row); else db.Entry(existing).CurrentValues.SetValues(row); await db.SaveChangesAsync(cancellationToken);
    }
    public async Task SaveAsync(ProjectExecutionHold hold, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var row = PersistenceMappings.ToRow(hold); var existing = await db.ProjectExecutionHolds.SingleOrDefaultAsync(x => x.Id == row.Id, cancellationToken);
        if (existing is null) db.Add(row); else db.Entry(existing).CurrentValues.SetValues(row); await db.SaveChangesAsync(cancellationToken);
    }
}

public sealed class SqliteSettingsRepository(IDbContextFactory<KischedulerDbContext> contextFactory) : ISettingsRepository
{
    private static readonly string[] SensitiveKeyParts = ["password", "secret", "token", "apikey", "api_key", "credential", "authorization"];
    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        ValidateKey(key); await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Settings.AsNoTracking().Where(x => x.Key == key.Trim()).Select(x => x.Value).SingleOrDefaultAsync(cancellationToken);
    }
    public async Task SetAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        ValidateKey(key); ArgumentNullException.ThrowIfNull(value);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var normalized = key.Trim(); var row = await db.Settings.SingleOrDefaultAsync(x => x.Key == normalized, cancellationToken);
        if (row is null) db.Settings.Add(new SettingRow { Key = normalized, Value = value }); else row.Value = value;
        await db.SaveChangesAsync(cancellationToken);
    }
    private static void ValidateKey(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (SensitiveKeyParts.Any(part => key.Contains(part, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Sensitive Einstellungen dürfen nicht in der lokalen Datenbank gespeichert werden.");
    }
}

public sealed class SqliteAtomicExecutionRepository(IDbContextFactory<KischedulerDbContext> contextFactory) : IAtomicExecutionRepository
{
    public async Task PersistUsageExceededAsync(UsageExceededPersistenceRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.WorkItem.Status != WorkItemStatus.WartetAufUsage || request.Attempt.Result != ExecutionAttemptResult.UsageExceeded)
            throw new ArgumentException("Auftrag und Versuch bilden kein UsageExceeded-Ergebnis.", nameof(request));
        if (request.WorkItem.Id != request.Attempt.WorkItemId || request.WorkItem.Id != request.Event.WorkItemId ||
            request.WorkItem.Id != request.PlatformBlock.TriggeringWorkItemId || request.WorkItem.Id != request.ProjectHold.TriggeringWorkItemId)
            throw new ArgumentException("Alle Datensätze müssen zum selben Auftrag gehören.", nameof(request));
        if (request.WorkItem.PlatformId != request.Attempt.PlatformId
            || request.WorkItem.PlatformId != request.PlatformBlock.PlatformId
            || request.WorkItem.PlatformProfileId != request.Attempt.PlatformProfileId
            || request.WorkItem.PlatformProfileId != request.Event.PlatformProfileId
            || request.WorkItem.PlatformProfileId != request.PlatformBlock.PlatformProfileId)
            throw new ArgumentException("Versuch und Usage-Sperre müssen zur Plattform- und Profilzuordnung des Auftrags gehören.",
                nameof(request));

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var existing = await db.WorkItems.SingleOrDefaultAsync(x => x.Id == request.WorkItem.Id.Value, cancellationToken)
            ?? throw new InvalidOperationException($"Auftrag '{request.WorkItem.Id}' wurde nicht gefunden.");
        db.Entry(existing).CurrentValues.SetValues(PersistenceMappings.ToRow(request.WorkItem));
        db.ExecutionAttempts.Add(PersistenceMappings.ToRow(request.Attempt));
        db.ExecutionEvents.Add(PersistenceMappings.ToRow(request.Event));
        db.PlatformUsageBlocks.Add(PersistenceMappings.ToRow(request.PlatformBlock));
        db.ProjectExecutionHolds.Add(PersistenceMappings.ToRow(request.ProjectHold));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
