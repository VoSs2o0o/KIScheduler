using Microsoft.EntityFrameworkCore;

namespace KIScheduler.Infrastructure.Persistence;

public sealed class KischedulerDbContext(DbContextOptions<KischedulerDbContext> options) : DbContext(options)
{
    public DbSet<WorkItemRow> WorkItems => Set<WorkItemRow>();
    public DbSet<ProjectRow> Projects => Set<ProjectRow>();
    public DbSet<PlatformRow> Platforms => Set<PlatformRow>();
    public DbSet<PlatformProfileRow> PlatformProfiles => Set<PlatformProfileRow>();
    public DbSet<UsagePolicyRow> UsagePolicies => Set<UsagePolicyRow>();
    public DbSet<UsageSnapshotRow> UsageSnapshots => Set<UsageSnapshotRow>();
    public DbSet<UsageWindowRow> UsageWindows => Set<UsageWindowRow>();
    public DbSet<ExecutionAttemptRow> ExecutionAttempts => Set<ExecutionAttemptRow>();
    public DbSet<ExecutionEventRow> ExecutionEvents => Set<ExecutionEventRow>();
    public DbSet<PlatformUsageBlockRow> PlatformUsageBlocks => Set<PlatformUsageBlockRow>();
    public DbSet<ProjectExecutionHoldRow> ProjectExecutionHolds => Set<ProjectExecutionHoldRow>();
    public DbSet<SchedulerLeaseRow> SchedulerLeases => Set<SchedulerLeaseRow>();
    public DbSet<SettingRow> Settings => Set<SettingRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WorkItemRow>(entity =>
        {
            entity.ToTable("WorkItems");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(500).IsRequired();
            entity.Property(x => x.PlatformId).HasMaxLength(100).IsRequired();
            entity.HasIndex(x => new { x.PlatformProfileId, x.PlatformId });
            entity.Property(x => x.ModelId).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Effort).HasMaxLength(50).IsRequired();
            entity.Property(x => x.PromptPath).HasMaxLength(2048).IsRequired();
            entity.Property(x => x.CommitMessage).HasMaxLength(1000);
            entity.HasIndex(x => new { x.Status, x.Priority, x.CreatedAtUtc });
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_WorkItems_Priority", "Priority >= 0 AND Priority <= 100");
                table.HasCheckConstraint("CK_WorkItems_NormalRetryCount", "NormalRetryCount >= 0");
            });
            entity.HasOne<ProjectRow>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<PlatformProfileRow>().WithMany()
                .HasForeignKey(x => new { x.PlatformProfileId, x.PlatformId })
                .HasPrincipalKey(x => new { x.Id, x.PlatformId })
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<ProjectRow>(entity =>
        {
            entity.ToTable("Projects"); entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(300).IsRequired();
            entity.Property(x => x.RootPath).HasMaxLength(2048).IsRequired();
            entity.Property(x => x.TargetBranch).HasMaxLength(255).IsRequired();
            entity.Property(x => x.DefaultTemplate).HasMaxLength(255).IsRequired();
            entity.Property(x => x.ValidationCommandsJson).IsRequired();
            entity.HasIndex(x => x.RootPath).IsUnique();
        });
        modelBuilder.Entity<PlatformRow>(entity =>
        {
            entity.ToTable("Platforms"); entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasMaxLength(100);
            entity.Property(x => x.Executable).HasMaxLength(2048).IsRequired();
            entity.Property(x => x.ModelsJson).IsRequired();
            entity.ToTable(table => table.HasCheckConstraint("CK_Platforms_Capacity", "Capacity > 0"));
        });
        modelBuilder.Entity<PlatformProfileRow>(entity =>
        {
            entity.ToTable("PlatformProfiles");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.PlatformId).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(100).UseCollation("NOCASE").IsRequired();
            entity.Property(x => x.DisplayName).HasMaxLength(300).UseCollation("NOCASE").IsRequired();
            entity.Property(x => x.ConfigurationDirectory).HasMaxLength(2048).UseCollation("NOCASE").IsRequired();
            entity.HasAlternateKey(x => new { x.Id, x.PlatformId });
            entity.HasIndex(x => new { x.PlatformId, x.Name }).IsUnique().HasFilter("\"Enabled\" = 1");
            entity.HasIndex(x => new { x.PlatformId, x.ConfigurationDirectory }).IsUnique().HasFilter("\"Enabled\" = 1");
            entity.HasIndex(x => new { x.PlatformId, x.DisplayName }).IsUnique().HasFilter("\"Enabled\" = 1");
            entity.HasIndex(x => x.PlatformId).IsUnique().HasFilter("\"IsDefault\" = 1 AND \"Enabled\" = 1");
            entity.HasOne<PlatformRow>().WithMany().HasForeignKey(x => x.PlatformId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<UsagePolicyRow>(entity =>
        {
            entity.ToTable("UsagePolicies"); entity.HasKey(x => x.Id);
            entity.Property(x => x.PlatformId).HasMaxLength(100).IsRequired();
            entity.Property(x => x.ModelId).HasMaxLength(100);
            entity.Property(x => x.TimeZoneId).HasMaxLength(200).IsRequired();
            entity.HasIndex(x => new { x.PlatformId, x.ModelId });
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_UsagePolicies_MaxUsedPercent",
                    "CAST(MaxUsedPercent AS REAL) >= 0 AND CAST(MaxUsedPercent AS REAL) <= 100");
                table.HasCheckConstraint("CK_UsagePolicies_RefreshInterval", "RefreshIntervalTicks > 0");
            });
        });
        modelBuilder.Entity<UsageSnapshotRow>(entity =>
        {
            entity.ToTable("UsageSnapshots"); entity.HasKey(x => x.Id);
            entity.Property(x => x.PlatformId).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Source).HasMaxLength(500).IsRequired();
            entity.HasIndex(x => new { x.PlatformProfileId, x.ReadAtUtc });
            entity.HasOne<PlatformProfileRow>().WithMany()
                .HasForeignKey(x => new { x.PlatformProfileId, x.PlatformId })
                .HasPrincipalKey(x => new { x.Id, x.PlatformId })
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<UsageWindowRow>(entity =>
        {
            entity.ToTable("UsageWindows"); entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.LimitId).HasMaxLength(200);
            entity.Property(x => x.LimitName).HasMaxLength(300);
            entity.Property(x => x.Source).HasMaxLength(500).IsRequired();
            entity.HasIndex(x => new { x.SnapshotId, x.Name }).IsUnique();
            entity.HasOne<UsageSnapshotRow>().WithMany().HasForeignKey(x => x.SnapshotId).OnDelete(DeleteBehavior.Cascade);
            entity.ToTable(table => table.HasCheckConstraint("CK_UsageWindows_UsedPercent",
                "CAST(UsedPercent AS REAL) >= 0 AND CAST(UsedPercent AS REAL) <= 100"));
        });
        modelBuilder.Entity<ExecutionAttemptRow>(entity =>
        {
            entity.ToTable("ExecutionAttempts"); entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.WorkItemId, x.SequenceNumber }).IsUnique();
            entity.HasOne<WorkItemRow>().WithMany().HasForeignKey(x => x.WorkItemId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<PlatformProfileRow>().WithMany()
                .HasForeignKey(x => new { x.PlatformProfileId, x.PlatformId })
                .HasPrincipalKey(x => new { x.Id, x.PlatformId })
                .OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table => table.HasCheckConstraint("CK_ExecutionAttempts_SequenceNumber", "SequenceNumber > 0"));
        });
        modelBuilder.Entity<ExecutionEventRow>(entity =>
        {
            entity.ToTable("ExecutionEvents"); entity.HasKey(x => x.Id);
            entity.Property(x => x.DataJson).IsRequired();
            entity.HasIndex(x => new { x.WorkItemId, x.OccurredAtUtc });
            entity.HasIndex(x => x.PlatformProfileId);
            entity.HasOne<WorkItemRow>().WithMany().HasForeignKey(x => x.WorkItemId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<ExecutionAttemptRow>().WithMany().HasForeignKey(x => x.AttemptId).OnDelete(DeleteBehavior.SetNull);
        });
        modelBuilder.Entity<PlatformUsageBlockRow>(entity =>
        {
            entity.ToTable("PlatformUsageBlocks"); entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.PlatformProfileId, x.ReleasedAtUtc });
            entity.HasOne<WorkItemRow>().WithMany().HasForeignKey(x => x.TriggeringWorkItemId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<PlatformProfileRow>().WithMany()
                .HasForeignKey(x => new { x.PlatformProfileId, x.PlatformId })
                .HasPrincipalKey(x => new { x.Id, x.PlatformId })
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<ProjectExecutionHoldRow>(entity =>
        {
            entity.ToTable("ProjectExecutionHolds"); entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.ProjectId, x.ReleasedAtUtc });
            entity.HasOne<ProjectRow>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<WorkItemRow>().WithMany().HasForeignKey(x => x.TriggeringWorkItemId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<SchedulerLeaseRow>(entity =>
        {
            entity.ToTable("SchedulerLeases"); entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.WorkItemId).IsUnique();
            entity.HasIndex(x => x.ExpiresAtUtc);
            entity.HasOne<WorkItemRow>().WithMany().HasForeignKey(x => x.WorkItemId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<SettingRow>(entity =>
        {
            entity.ToTable("Settings"); entity.HasKey(x => x.Key);
            entity.Property(x => x.Key).HasMaxLength(200);
            entity.Property(x => x.Value).IsRequired();
        });
    }

}

public sealed class WorkItemRow
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public int Priority { get; set; }
    public string PlatformId { get; set; } = "";
    public Guid PlatformProfileId { get; set; }
    public string ModelId { get; set; } = "";
    public string Effort { get; set; } = "";
    public string PromptPath { get; set; } = "";
    public bool AutoCommit { get; set; }
    public string? CommitMessage { get; set; }
    public Guid? ProjectId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? FirstAttemptStartedAtUtc { get; set; }
    public bool HasExecutionStarted { get; set; }
    public int Status { get; set; }
    public int NormalRetryCount { get; set; }
}

public sealed class ProjectRow
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string RootPath { get; set; } = "";
    public string TargetBranch { get; set; } = "";
    public string DefaultTemplate { get; set; } = "classlib";
    public string ValidationCommandsJson { get; set; } = "[]";
}

public sealed class PlatformRow
{
    public string Id { get; set; } = "";
    public string Executable { get; set; } = "";
    public int Capacity { get; set; }
    public bool Enabled { get; set; } = true;
    public bool ShowUsageInStatusBar { get; set; }
    public string ModelsJson { get; set; } = "[]";
}

public sealed class PlatformProfileRow
{
    public Guid Id { get; set; }
    public string PlatformId { get; set; } = "";
    public string Name { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string ConfigurationDirectory { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public bool IsDefault { get; set; }
    public bool ShowUsageInStatusBar { get; set; }
}

public sealed class UsagePolicyRow
{
    public Guid Id { get; set; }
    public string PlatformId { get; set; } = "";
    public string? ModelId { get; set; }
    public int DaysMask { get; set; }
    public long LocalStartTicks { get; set; }
    public long LocalEndTicks { get; set; }
    public string TimeZoneId { get; set; } = "";
    public decimal MaxUsedPercent { get; set; }
    public int UnknownUsageBehavior { get; set; }
    public long RefreshIntervalTicks { get; set; }
    public long? EndSprintDurationTicks { get; set; }
    public decimal? EndSprintMaxUsedPercent { get; set; }
}

public sealed class UsageSnapshotRow
{
    public Guid Id { get; set; }
    public string PlatformId { get; set; } = "";
    public Guid PlatformProfileId { get; set; }
    public DateTimeOffset ReadAtUtc { get; set; }
    public string Source { get; set; } = "";
    public int Quality { get; set; }
}

public sealed class UsageWindowRow
{
    public Guid Id { get; set; }
    public Guid SnapshotId { get; set; }
    public string Name { get; set; } = "";
    public string? LimitId { get; set; }
    public string? LimitName { get; set; }
    public decimal UsedPercent { get; set; }
    public DateTimeOffset? ResetAtUtc { get; set; }
    public string Source { get; set; } = "";
    public DateTimeOffset ReadAtUtc { get; set; }
    public int Quality { get; set; }
    public string? RateLimitReachedType { get; set; }
    public long? WindowDurationTicks { get; set; }
}

public sealed class ExecutionAttemptRow
{
    public Guid Id { get; set; }
    public Guid WorkItemId { get; set; }
    public int SequenceNumber { get; set; }
    public string PlatformId { get; set; } = "";
    public Guid PlatformProfileId { get; set; }
    public string ModelId { get; set; } = "";
    public string Effort { get; set; } = "";
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset CompletedAtUtc { get; set; }
    public int Result { get; set; }
    public int? ExitCode { get; set; }
    public string? SessionId { get; set; }
    public string? Diagnostic { get; set; }
}

public sealed class ExecutionEventRow
{
    public Guid Id { get; set; }
    public Guid WorkItemId { get; set; }
    public Guid PlatformProfileId { get; set; }
    public Guid? AttemptId { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
    public int Severity { get; set; }
    public string EventType { get; set; } = "";
    public string Message { get; set; } = "";
    public string DataJson { get; set; } = "{}";
}

public sealed class PlatformUsageBlockRow
{
    public Guid Id { get; set; }
    public string PlatformId { get; set; } = "";
    public Guid PlatformProfileId { get; set; }
    public Guid TriggeringWorkItemId { get; set; }
    public Guid? TriggeringAttemptId { get; set; }
    public string Reason { get; set; } = "";
    public DateTimeOffset CreatedAtUtc { get; set; }
    public int ReleaseRule { get; set; }
    public DateTimeOffset? ReleasedAtUtc { get; set; }
    public string? ReleaseReason { get; set; }
}

public sealed class ProjectExecutionHoldRow
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid TriggeringWorkItemId { get; set; }
    public string PlatformId { get; set; } = "";
    public string Reason { get; set; } = "";
    public DateTimeOffset CreatedAtUtc { get; set; }
    public int ReleaseRule { get; set; }
    public DateTimeOffset? ReleasedAtUtc { get; set; }
    public string? ReleaseReason { get; set; }
}

public sealed class SchedulerLeaseRow
{
    public Guid Id { get; set; }
    public Guid WorkItemId { get; set; }
    public string OwnerId { get; set; } = "";
    public DateTimeOffset AcquiredAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
}

public sealed class SettingRow
{
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
}
