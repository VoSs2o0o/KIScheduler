using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KIScheduler.Infrastructure.Persistence.Migrations;

[DbContext(typeof(KischedulerDbContext))]
[Migration("20260911150000_FixUsagePercentConstraints")]
public sealed class FixUsagePercentConstraints : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        RebuildUsagePolicies(migrationBuilder,
            "CAST(MaxUsedPercent AS REAL) >= 0 AND CAST(MaxUsedPercent AS REAL) <= 100");
        RebuildUsageWindows(migrationBuilder,
            "CAST(UsedPercent AS REAL) >= 0 AND CAST(UsedPercent AS REAL) <= 100");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        RebuildUsagePolicies(migrationBuilder, "MaxUsedPercent >= 0 AND MaxUsedPercent <= 100");
        RebuildUsageWindows(migrationBuilder, "UsedPercent >= 0 AND UsedPercent <= 100");
    }

    private static void RebuildUsagePolicies(MigrationBuilder migrationBuilder, string constraint)
    {
        migrationBuilder.Sql($$"""
            CREATE TABLE "__temp_UsagePolicies" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_UsagePolicies" PRIMARY KEY,
                "PlatformId" TEXT NOT NULL,
                "ModelId" TEXT NULL,
                "DaysMask" INTEGER NOT NULL,
                "LocalStartTicks" INTEGER NOT NULL,
                "LocalEndTicks" INTEGER NOT NULL,
                "TimeZoneId" TEXT NOT NULL,
                "MaxUsedPercent" TEXT NOT NULL,
                "UnknownUsageBehavior" INTEGER NOT NULL,
                "RefreshIntervalTicks" INTEGER NOT NULL,
                "EndSprintDurationTicks" INTEGER NULL,
                "EndSprintMaxUsedPercent" TEXT NULL,
                CONSTRAINT "CK_UsagePolicies_MaxUsedPercent" CHECK ({{constraint}}),
                CONSTRAINT "CK_UsagePolicies_RefreshInterval" CHECK (RefreshIntervalTicks > 0)
            );
            INSERT INTO "__temp_UsagePolicies" SELECT "Id", "PlatformId", "ModelId", "DaysMask",
                "LocalStartTicks", "LocalEndTicks", "TimeZoneId", "MaxUsedPercent", "UnknownUsageBehavior",
                "RefreshIntervalTicks", "EndSprintDurationTicks", "EndSprintMaxUsedPercent" FROM "UsagePolicies";
            DROP TABLE "UsagePolicies";
            ALTER TABLE "__temp_UsagePolicies" RENAME TO "UsagePolicies";
            CREATE INDEX "IX_UsagePolicies_PlatformId_ModelId" ON "UsagePolicies" ("PlatformId", "ModelId");
            """);
    }

    private static void RebuildUsageWindows(MigrationBuilder migrationBuilder, string constraint)
    {
        migrationBuilder.Sql($$"""
            CREATE TABLE "__temp_UsageWindows" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_UsageWindows" PRIMARY KEY,
                "SnapshotId" TEXT NOT NULL,
                "Name" TEXT NOT NULL,
                "UsedPercent" TEXT NOT NULL,
                "ResetAtUtc" TEXT NULL,
                "Source" TEXT NOT NULL,
                "ReadAtUtc" TEXT NOT NULL,
                "Quality" INTEGER NOT NULL,
                "RateLimitReachedType" TEXT NULL,
                "WindowDurationTicks" INTEGER NULL,
                CONSTRAINT "CK_UsageWindows_UsedPercent" CHECK ({{constraint}}),
                CONSTRAINT "FK_UsageWindows_UsageSnapshots_SnapshotId" FOREIGN KEY ("SnapshotId")
                    REFERENCES "UsageSnapshots" ("Id") ON DELETE CASCADE
            );
            INSERT INTO "__temp_UsageWindows" SELECT "Id", "SnapshotId", "Name", "UsedPercent", "ResetAtUtc",
                "Source", "ReadAtUtc", "Quality", "RateLimitReachedType", "WindowDurationTicks" FROM "UsageWindows";
            DROP TABLE "UsageWindows";
            ALTER TABLE "__temp_UsageWindows" RENAME TO "UsageWindows";
            CREATE UNIQUE INDEX "IX_UsageWindows_SnapshotId_Name" ON "UsageWindows" ("SnapshotId", "Name");
            """);
    }
}
