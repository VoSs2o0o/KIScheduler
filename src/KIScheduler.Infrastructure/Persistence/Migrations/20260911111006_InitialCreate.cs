using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KIScheduler.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Platforms",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Executable = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    Capacity = table.Column<int>(type: "INTEGER", nullable: false),
                    ModelsJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Platforms", x => x.Id);
                    table.CheckConstraint("CK_Platforms_Capacity", "Capacity > 0");
                });

            migrationBuilder.CreateTable(
                name: "Projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    RootPath = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    TargetBranch = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    ValidationCommandsJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Settings",
                columns: table => new
                {
                    Key = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settings", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "UsagePolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PlatformId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ModelId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    DaysMask = table.Column<int>(type: "INTEGER", nullable: false),
                    LocalStartTicks = table.Column<long>(type: "INTEGER", nullable: false),
                    LocalEndTicks = table.Column<long>(type: "INTEGER", nullable: false),
                    TimeZoneId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    MaxUsedPercent = table.Column<decimal>(type: "TEXT", nullable: false),
                    UnknownUsageBehavior = table.Column<int>(type: "INTEGER", nullable: false),
                    RefreshIntervalTicks = table.Column<long>(type: "INTEGER", nullable: false),
                    EndSprintDurationTicks = table.Column<long>(type: "INTEGER", nullable: true),
                    EndSprintMaxUsedPercent = table.Column<decimal>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsagePolicies", x => x.Id);
                    table.CheckConstraint("CK_UsagePolicies_MaxUsedPercent", "MaxUsedPercent >= 0 AND MaxUsedPercent <= 100");
                    table.CheckConstraint("CK_UsagePolicies_RefreshInterval", "RefreshIntervalTicks > 0");
                });

            migrationBuilder.CreateTable(
                name: "UsageSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PlatformId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ReadAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Quality = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsageSnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    PlatformId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ModelId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Effort = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    PromptPath = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    AutoCommit = table.Column<bool>(type: "INTEGER", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    FirstAttemptStartedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    HasExecutionStarted = table.Column<bool>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    NormalRetryCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkItems", x => x.Id);
                    table.CheckConstraint("CK_WorkItems_NormalRetryCount", "NormalRetryCount >= 0");
                    table.CheckConstraint("CK_WorkItems_Priority", "Priority >= 0 AND Priority <= 100");
                    table.ForeignKey(
                        name: "FK_WorkItems_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UsageWindows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SnapshotId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    UsedPercent = table.Column<decimal>(type: "TEXT", nullable: false),
                    ResetAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    Source = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ReadAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Quality = table.Column<int>(type: "INTEGER", nullable: false),
                    RateLimitReachedType = table.Column<string>(type: "TEXT", nullable: true),
                    WindowDurationTicks = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsageWindows", x => x.Id);
                    table.CheckConstraint("CK_UsageWindows_UsedPercent", "UsedPercent >= 0 AND UsedPercent <= 100");
                    table.ForeignKey(
                        name: "FK_UsageWindows_UsageSnapshots_SnapshotId",
                        column: x => x.SnapshotId,
                        principalTable: "UsageSnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExecutionAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SequenceNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    PlatformId = table.Column<string>(type: "TEXT", nullable: false),
                    ModelId = table.Column<string>(type: "TEXT", nullable: false),
                    Effort = table.Column<string>(type: "TEXT", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Result = table.Column<int>(type: "INTEGER", nullable: false),
                    ExitCode = table.Column<int>(type: "INTEGER", nullable: true),
                    SessionId = table.Column<string>(type: "TEXT", nullable: true),
                    Diagnostic = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExecutionAttempts", x => x.Id);
                    table.CheckConstraint("CK_ExecutionAttempts_SequenceNumber", "SequenceNumber > 0");
                    table.ForeignKey(
                        name: "FK_ExecutionAttempts_WorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlatformUsageBlocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PlatformId = table.Column<string>(type: "TEXT", nullable: false),
                    TriggeringWorkItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TriggeringAttemptId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Reason = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ReleaseRule = table.Column<int>(type: "INTEGER", nullable: false),
                    ReleasedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ReleaseReason = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformUsageBlocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlatformUsageBlocks_WorkItems_TriggeringWorkItemId",
                        column: x => x.TriggeringWorkItemId,
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectExecutionHolds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TriggeringWorkItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PlatformId = table.Column<string>(type: "TEXT", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ReleaseRule = table.Column<int>(type: "INTEGER", nullable: false),
                    ReleasedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ReleaseReason = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectExecutionHolds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectExecutionHolds_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectExecutionHolds_WorkItems_TriggeringWorkItemId",
                        column: x => x.TriggeringWorkItemId,
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SchedulerLeases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OwnerId = table.Column<string>(type: "TEXT", nullable: false),
                    AcquiredAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchedulerLeases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SchedulerLeases_WorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExecutionEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AttemptId = table.Column<Guid>(type: "TEXT", nullable: true),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Severity = table.Column<int>(type: "INTEGER", nullable: false),
                    EventType = table.Column<string>(type: "TEXT", nullable: false),
                    Message = table.Column<string>(type: "TEXT", nullable: false),
                    DataJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExecutionEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExecutionEvents_ExecutionAttempts_AttemptId",
                        column: x => x.AttemptId,
                        principalTable: "ExecutionAttempts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ExecutionEvents_WorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionAttempts_WorkItemId_SequenceNumber",
                table: "ExecutionAttempts",
                columns: new[] { "WorkItemId", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionEvents_AttemptId",
                table: "ExecutionEvents",
                column: "AttemptId");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionEvents_WorkItemId_OccurredAtUtc",
                table: "ExecutionEvents",
                columns: new[] { "WorkItemId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformUsageBlocks_PlatformId_ReleasedAtUtc",
                table: "PlatformUsageBlocks",
                columns: new[] { "PlatformId", "ReleasedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformUsageBlocks_TriggeringWorkItemId",
                table: "PlatformUsageBlocks",
                column: "TriggeringWorkItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectExecutionHolds_ProjectId_ReleasedAtUtc",
                table: "ProjectExecutionHolds",
                columns: new[] { "ProjectId", "ReleasedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectExecutionHolds_TriggeringWorkItemId",
                table: "ProjectExecutionHolds",
                column: "TriggeringWorkItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_RootPath",
                table: "Projects",
                column: "RootPath",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SchedulerLeases_ExpiresAtUtc",
                table: "SchedulerLeases",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulerLeases_WorkItemId",
                table: "SchedulerLeases",
                column: "WorkItemId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UsagePolicies_PlatformId_ModelId",
                table: "UsagePolicies",
                columns: new[] { "PlatformId", "ModelId" });

            migrationBuilder.CreateIndex(
                name: "IX_UsageSnapshots_PlatformId_ReadAtUtc",
                table: "UsageSnapshots",
                columns: new[] { "PlatformId", "ReadAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_UsageWindows_SnapshotId_Name",
                table: "UsageWindows",
                columns: new[] { "SnapshotId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_ProjectId",
                table: "WorkItems",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_Status_Priority_CreatedAtUtc",
                table: "WorkItems",
                columns: new[] { "Status", "Priority", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExecutionEvents");

            migrationBuilder.DropTable(
                name: "Platforms");

            migrationBuilder.DropTable(
                name: "PlatformUsageBlocks");

            migrationBuilder.DropTable(
                name: "ProjectExecutionHolds");

            migrationBuilder.DropTable(
                name: "SchedulerLeases");

            migrationBuilder.DropTable(
                name: "Settings");

            migrationBuilder.DropTable(
                name: "UsagePolicies");

            migrationBuilder.DropTable(
                name: "UsageWindows");

            migrationBuilder.DropTable(
                name: "ExecutionAttempts");

            migrationBuilder.DropTable(
                name: "UsageSnapshots");

            migrationBuilder.DropTable(
                name: "WorkItems");

            migrationBuilder.DropTable(
                name: "Projects");
        }
    }
}
