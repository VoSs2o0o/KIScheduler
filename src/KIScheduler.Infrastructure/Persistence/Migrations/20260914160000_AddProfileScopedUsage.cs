using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KIScheduler.Infrastructure.Persistence.Migrations;

[DbContext(typeof(KischedulerDbContext))]
[Migration("20260914160000_AddProfileScopedUsage")]
public partial class AddProfileScopedUsage : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("PRAGMA foreign_keys = OFF;", suppressTransaction: true);
        migrationBuilder.Sql("""
            -- AP13 generated default-profile GUIDs in lower-case SQL text. EF's SQLite GUID
            -- parameters use upper-case text, so canonicalize the profile key and every existing
            -- reference before adding further profile foreign keys.
            UPDATE "PlatformProfiles" SET "Id" = upper("Id");
            UPDATE "WorkItems" SET "PlatformProfileId" = upper("PlatformProfileId");
            UPDATE "ExecutionAttempts" SET "PlatformProfileId" = upper("PlatformProfileId");

            CREATE TABLE "__temp_ExecutionEvents" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_ExecutionEvents" PRIMARY KEY,
                "WorkItemId" TEXT NOT NULL,
                "PlatformProfileId" TEXT NOT NULL,
                "AttemptId" TEXT NULL,
                "OccurredAtUtc" TEXT NOT NULL,
                "Severity" INTEGER NOT NULL,
                "EventType" TEXT NOT NULL,
                "Message" TEXT NOT NULL,
                "DataJson" TEXT NOT NULL,
                CONSTRAINT "FK_ExecutionEvents_WorkItems_WorkItemId"
                    FOREIGN KEY ("WorkItemId") REFERENCES "WorkItems" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_ExecutionEvents_ExecutionAttempts_AttemptId"
                    FOREIGN KEY ("AttemptId") REFERENCES "ExecutionAttempts" ("Id") ON DELETE SET NULL
            );
            INSERT INTO "__temp_ExecutionEvents"
                ("Id", "WorkItemId", "PlatformProfileId", "AttemptId", "OccurredAtUtc", "Severity",
                 "EventType", "Message", "DataJson")
            SELECT e."Id", e."WorkItemId", w."PlatformProfileId", e."AttemptId", e."OccurredAtUtc",
                   e."Severity", e."EventType", e."Message", e."DataJson"
            FROM "ExecutionEvents" AS e
            INNER JOIN "WorkItems" AS w ON w."Id" = e."WorkItemId";
            DROP TABLE "ExecutionEvents";
            ALTER TABLE "__temp_ExecutionEvents" RENAME TO "ExecutionEvents";
            CREATE INDEX "IX_ExecutionEvents_AttemptId" ON "ExecutionEvents" ("AttemptId");
            CREATE INDEX "IX_ExecutionEvents_PlatformProfileId" ON "ExecutionEvents" ("PlatformProfileId");
            CREATE INDEX "IX_ExecutionEvents_WorkItemId_OccurredAtUtc"
                ON "ExecutionEvents" ("WorkItemId", "OccurredAtUtc");

            CREATE TABLE "__temp_UsageSnapshots" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_UsageSnapshots" PRIMARY KEY,
                "PlatformId" TEXT NOT NULL,
                "PlatformProfileId" TEXT NOT NULL,
                "ReadAtUtc" TEXT NOT NULL,
                "Source" TEXT NOT NULL,
                "Quality" INTEGER NOT NULL,
                CONSTRAINT "FK_UsageSnapshots_PlatformProfiles_PlatformProfileId_PlatformId"
                    FOREIGN KEY ("PlatformProfileId", "PlatformId")
                    REFERENCES "PlatformProfiles" ("Id", "PlatformId") ON DELETE RESTRICT
            );
            INSERT INTO "__temp_UsageSnapshots"
                ("Id", "PlatformId", "PlatformProfileId", "ReadAtUtc", "Source", "Quality")
            SELECT s."Id", s."PlatformId",
                   (SELECT p."Id" FROM "PlatformProfiles" AS p
                    WHERE p."PlatformId" = s."PlatformId" AND p."IsDefault" = 1),
                   s."ReadAtUtc", s."Source", s."Quality"
            FROM "UsageSnapshots" AS s;
            DROP TABLE "UsageSnapshots";
            ALTER TABLE "__temp_UsageSnapshots" RENAME TO "UsageSnapshots";
            CREATE INDEX "IX_UsageSnapshots_PlatformProfileId_ReadAtUtc"
                ON "UsageSnapshots" ("PlatformProfileId", "ReadAtUtc");
            CREATE INDEX "IX_UsageSnapshots_PlatformProfileId_PlatformId"
                ON "UsageSnapshots" ("PlatformProfileId", "PlatformId");

            CREATE TABLE "__temp_PlatformUsageBlocks" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_PlatformUsageBlocks" PRIMARY KEY,
                "PlatformId" TEXT NOT NULL,
                "PlatformProfileId" TEXT NOT NULL,
                "TriggeringWorkItemId" TEXT NOT NULL,
                "TriggeringAttemptId" TEXT NULL,
                "Reason" TEXT NOT NULL,
                "CreatedAtUtc" TEXT NOT NULL,
                "ReleaseRule" INTEGER NOT NULL,
                "ReleasedAtUtc" TEXT NULL,
                "ReleaseReason" TEXT NULL,
                CONSTRAINT "FK_PlatformUsageBlocks_WorkItems_TriggeringWorkItemId"
                    FOREIGN KEY ("TriggeringWorkItemId") REFERENCES "WorkItems" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_PlatformUsageBlocks_PlatformProfiles_PlatformProfileId_PlatformId"
                    FOREIGN KEY ("PlatformProfileId", "PlatformId")
                    REFERENCES "PlatformProfiles" ("Id", "PlatformId") ON DELETE RESTRICT
            );
            INSERT INTO "__temp_PlatformUsageBlocks"
                ("Id", "PlatformId", "PlatformProfileId", "TriggeringWorkItemId", "TriggeringAttemptId",
                 "Reason", "CreatedAtUtc", "ReleaseRule", "ReleasedAtUtc", "ReleaseReason")
            SELECT b."Id", b."PlatformId",
                   COALESCE((SELECT w."PlatformProfileId" FROM "WorkItems" AS w
                             WHERE w."Id" = b."TriggeringWorkItemId"),
                            (SELECT p."Id" FROM "PlatformProfiles" AS p
                             WHERE p."PlatformId" = b."PlatformId" AND p."IsDefault" = 1)),
                   b."TriggeringWorkItemId", b."TriggeringAttemptId", b."Reason", b."CreatedAtUtc",
                   b."ReleaseRule", b."ReleasedAtUtc", b."ReleaseReason"
            FROM "PlatformUsageBlocks" AS b;
            DROP TABLE "PlatformUsageBlocks";
            ALTER TABLE "__temp_PlatformUsageBlocks" RENAME TO "PlatformUsageBlocks";
            CREATE INDEX "IX_PlatformUsageBlocks_PlatformProfileId_ReleasedAtUtc"
                ON "PlatformUsageBlocks" ("PlatformProfileId", "ReleasedAtUtc");
            CREATE INDEX "IX_PlatformUsageBlocks_PlatformProfileId_PlatformId"
                ON "PlatformUsageBlocks" ("PlatformProfileId", "PlatformId");
            CREATE INDEX "IX_PlatformUsageBlocks_TriggeringWorkItemId"
                ON "PlatformUsageBlocks" ("TriggeringWorkItemId");
            """);
        migrationBuilder.Sql("PRAGMA foreign_keys = ON;", suppressTransaction: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("PRAGMA foreign_keys = OFF;", suppressTransaction: true);
        migrationBuilder.Sql("""
            CREATE TABLE "__temp_ExecutionEvents" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_ExecutionEvents" PRIMARY KEY,
                "WorkItemId" TEXT NOT NULL,
                "AttemptId" TEXT NULL,
                "OccurredAtUtc" TEXT NOT NULL,
                "Severity" INTEGER NOT NULL,
                "EventType" TEXT NOT NULL,
                "Message" TEXT NOT NULL,
                "DataJson" TEXT NOT NULL,
                CONSTRAINT "FK_ExecutionEvents_WorkItems_WorkItemId"
                    FOREIGN KEY ("WorkItemId") REFERENCES "WorkItems" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_ExecutionEvents_ExecutionAttempts_AttemptId"
                    FOREIGN KEY ("AttemptId") REFERENCES "ExecutionAttempts" ("Id") ON DELETE SET NULL
            );
            INSERT INTO "__temp_ExecutionEvents"
                ("Id", "WorkItemId", "AttemptId", "OccurredAtUtc", "Severity", "EventType", "Message", "DataJson")
            SELECT "Id", "WorkItemId", "AttemptId", "OccurredAtUtc", "Severity", "EventType", "Message", "DataJson"
            FROM "ExecutionEvents";
            DROP TABLE "ExecutionEvents";
            ALTER TABLE "__temp_ExecutionEvents" RENAME TO "ExecutionEvents";
            CREATE INDEX "IX_ExecutionEvents_AttemptId" ON "ExecutionEvents" ("AttemptId");
            CREATE INDEX "IX_ExecutionEvents_WorkItemId_OccurredAtUtc"
                ON "ExecutionEvents" ("WorkItemId", "OccurredAtUtc");

            CREATE TABLE "__temp_PlatformUsageBlocks" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_PlatformUsageBlocks" PRIMARY KEY,
                "PlatformId" TEXT NOT NULL,
                "TriggeringWorkItemId" TEXT NOT NULL,
                "TriggeringAttemptId" TEXT NULL,
                "Reason" TEXT NOT NULL,
                "CreatedAtUtc" TEXT NOT NULL,
                "ReleaseRule" INTEGER NOT NULL,
                "ReleasedAtUtc" TEXT NULL,
                "ReleaseReason" TEXT NULL,
                CONSTRAINT "FK_PlatformUsageBlocks_WorkItems_TriggeringWorkItemId"
                    FOREIGN KEY ("TriggeringWorkItemId") REFERENCES "WorkItems" ("Id") ON DELETE RESTRICT
            );
            INSERT INTO "__temp_PlatformUsageBlocks"
                ("Id", "PlatformId", "TriggeringWorkItemId", "TriggeringAttemptId", "Reason",
                 "CreatedAtUtc", "ReleaseRule", "ReleasedAtUtc", "ReleaseReason")
            SELECT "Id", "PlatformId", "TriggeringWorkItemId", "TriggeringAttemptId", "Reason",
                   "CreatedAtUtc", "ReleaseRule", "ReleasedAtUtc", "ReleaseReason"
            FROM "PlatformUsageBlocks";
            DROP TABLE "PlatformUsageBlocks";
            ALTER TABLE "__temp_PlatformUsageBlocks" RENAME TO "PlatformUsageBlocks";
            CREATE INDEX "IX_PlatformUsageBlocks_PlatformId_ReleasedAtUtc"
                ON "PlatformUsageBlocks" ("PlatformId", "ReleasedAtUtc");
            CREATE INDEX "IX_PlatformUsageBlocks_TriggeringWorkItemId"
                ON "PlatformUsageBlocks" ("TriggeringWorkItemId");

            CREATE TABLE "__temp_UsageSnapshots" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_UsageSnapshots" PRIMARY KEY,
                "PlatformId" TEXT NOT NULL,
                "ReadAtUtc" TEXT NOT NULL,
                "Source" TEXT NOT NULL,
                "Quality" INTEGER NOT NULL
            );
            INSERT INTO "__temp_UsageSnapshots" ("Id", "PlatformId", "ReadAtUtc", "Source", "Quality")
            SELECT "Id", "PlatformId", "ReadAtUtc", "Source", "Quality" FROM "UsageSnapshots";
            DROP TABLE "UsageSnapshots";
            ALTER TABLE "__temp_UsageSnapshots" RENAME TO "UsageSnapshots";
            CREATE INDEX "IX_UsageSnapshots_PlatformId_ReadAtUtc"
                ON "UsageSnapshots" ("PlatformId", "ReadAtUtc");
            """);
        migrationBuilder.Sql("PRAGMA foreign_keys = ON;", suppressTransaction: true);
    }
}
