using KIScheduler.Core.Contracts;
using KIScheduler.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace KIScheduler.Infrastructure.Persistence;

public sealed class SqliteWorkItemRepository(IDbContextFactory<KischedulerDbContext> contextFactory) : IWorkItemRepository
{
    public async Task<WorkItem?> GetAsync(WorkItemId id, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.WorkItems.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id.Value, cancellationToken);
        return row is null ? null : PersistenceMappings.ToDomain(row);
    }

    public async Task<IReadOnlyList<WorkItem>> ListByStatusAsync(IReadOnlyCollection<WorkItemStatus> statuses,
        CancellationToken cancellationToken = default)
    {
        if (statuses.Count == 0) return [];
        var values = statuses.Select(x => (int)x).ToArray();
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await db.WorkItems.AsNoTracking().Where(x => values.Contains(x.Status)).ToListAsync(cancellationToken);
        return rows.OrderByDescending(x => x.Priority).ThenBy(x => x.CreatedAtUtc)
            .Select(PersistenceMappings.ToDomain).ToList();
    }

    public async Task SaveAsync(WorkItem workItem, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workItem);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await db.WorkItems.SingleOrDefaultAsync(x => x.Id == workItem.Id.Value, cancellationToken);
        if (existing is null) db.WorkItems.Add(PersistenceMappings.ToRow(workItem));
        else db.Entry(existing).CurrentValues.SetValues(PersistenceMappings.ToRow(workItem));
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<SchedulerLease?> TryAcquireLeaseAsync(WorkItemId workItemId, string ownerId,
        DateTimeOffset acquiredAtUtc, TimeSpan duration, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        if (acquiredAtUtc.Offset != TimeSpan.Zero) throw new ArgumentException("Zeitangaben müssen in UTC vorliegen.", nameof(acquiredAtUtc));
        if (duration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(duration));
        var lease = new SchedulerLease(SchedulerLeaseId.New(), workItemId, ownerId, acquiredAtUtc, acquiredAtUtc + duration);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM SchedulerLeases WHERE ExpiresAtUtc <= {acquiredAtUtc}", cancellationToken);
            var changed = await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                UPDATE WorkItems
                SET Status = {(int)WorkItemStatus.Reserviert}
                WHERE Id = {workItemId.Value}
                  AND Status IN ({(int)WorkItemStatus.InWarteschlange}, {(int)WorkItemStatus.WartetAufUsage})
                  AND NOT EXISTS (
                      SELECT 1
                      FROM SchedulerLeases AS lease
                      INNER JOIN WorkItems AS active ON active.Id = lease.WorkItemId
                      WHERE lease.ExpiresAtUtc > {acquiredAtUtc}
                        AND (
                            active.PlatformId = (SELECT candidate.PlatformId FROM WorkItems AS candidate WHERE candidate.Id = {workItemId.Value})
                            OR (
                                (SELECT candidate.ProjectId FROM WorkItems AS candidate WHERE candidate.Id = {workItemId.Value}) IS NOT NULL
                                AND active.ProjectId = (SELECT candidate.ProjectId FROM WorkItems AS candidate WHERE candidate.Id = {workItemId.Value})
                            )
                        )
                  )
                """, cancellationToken);
            if (changed != 1 || await db.SchedulerLeases.AnyAsync(x => x.WorkItemId == workItemId.Value, cancellationToken))
            {
                await transaction.RollbackAsync(cancellationToken); return null;
            }
            db.SchedulerLeases.Add(new SchedulerLeaseRow
            {
                Id = lease.Id.Value,
                WorkItemId = workItemId.Value,
                OwnerId = lease.OwnerId,
                AcquiredAtUtc = lease.AcquiredAtUtc,
                ExpiresAtUtc = lease.ExpiresAtUtc
            });
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return lease;
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken); return null;
        }
    }

    public async Task ReleaseLeaseAsync(SchedulerLeaseId leaseId, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await db.SchedulerLeases.Where(x => x.Id == leaseId.Value).ExecuteDeleteAsync(cancellationToken);
    }
}

public sealed class SqliteProjectRepository(IDbContextFactory<KischedulerDbContext> contextFactory) : IProjectRepository
{
    public async Task<ProjectDefinition?> GetAsync(ProjectId id, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.Projects.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id.Value, cancellationToken);
        return row is null ? null : ToDomain(row);
    }
    public async Task<IReadOnlyList<ProjectDefinition>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return (await db.Projects.AsNoTracking().OrderBy(x => x.Name).ToListAsync(cancellationToken)).Select(ToDomain).ToList();
    }
    public async Task SaveAsync(ProjectDefinition project, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var row = new ProjectRow
        {
            Id = project.Id.Value,
            Name = project.Name,
            RootPath = project.RootPath,
            TargetBranch = project.TargetBranch,
            DefaultTemplate = project.DefaultTemplate,
            ValidationCommandsJson = PersistenceMappings.Serialize(project.ValidationCommands
                .Select(x => new ValidationCommandData(x.Executable, x.Arguments.ToArray(), x.Required)))
        };
        var existing = await db.Projects.SingleOrDefaultAsync(x => x.Id == row.Id, cancellationToken);
        if (existing is null) db.Projects.Add(row); else db.Entry(existing).CurrentValues.SetValues(row);
        await db.SaveChangesAsync(cancellationToken);
    }
    private static ProjectDefinition ToDomain(ProjectRow row) => new(new(row.Id), row.Name, row.RootPath, row.TargetBranch,
        (PersistenceMappings.Deserialize<List<ValidationCommandData>>(row.ValidationCommandsJson) ?? [])
            .Select(x => new ValidationCommand(x.Executable, x.Arguments, x.Required)), row.DefaultTemplate);
    private sealed record ValidationCommandData(string Executable, string[] Arguments, bool Required);
}

public sealed class SqlitePlatformRepository(IDbContextFactory<KischedulerDbContext> contextFactory) : IPlatformRepository
{
    public async Task<PlatformDefinition?> GetAsync(PlatformId id, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.Platforms.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id.Value, cancellationToken);
        return row is null ? null : ToDomain(row);
    }
    public async Task<IReadOnlyList<PlatformDefinition>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return (await db.Platforms.AsNoTracking().OrderBy(x => x.Id).ToListAsync(cancellationToken)).Select(ToDomain).ToList();
    }
    public async Task SaveAsync(PlatformDefinition platform, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var row = new PlatformRow
        {
            Id = platform.Id.Value,
            Executable = platform.Executable,
            Capacity = platform.Capacity,
            ModelsJson = PersistenceMappings.Serialize(platform.Models.Select(x => new ModelData(x.Id.Value,
                x.SupportedEfforts.Select(e => e.Value).ToArray())))
        };
        var existing = await db.Platforms.SingleOrDefaultAsync(x => x.Id == row.Id, cancellationToken);
        if (existing is null) db.Platforms.Add(row); else db.Entry(existing).CurrentValues.SetValues(row);
        await db.SaveChangesAsync(cancellationToken);
    }
    private static PlatformDefinition ToDomain(PlatformRow row) => new(new(row.Id), row.Executable,
        (PersistenceMappings.Deserialize<List<ModelData>>(row.ModelsJson) ?? [])
            .Select(x => new PlatformModel(new(x.Id), x.Efforts.Select(e => new EffortLevel(e)))), row.Capacity);
    private sealed record ModelData(string Id, string[] Efforts);
}
