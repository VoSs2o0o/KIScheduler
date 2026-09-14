using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KIScheduler.Infrastructure.Persistence.Migrations;

[DbContext(typeof(KischedulerDbContext))]
[Migration("20260914100000_AddPlatformProfiles")]
public partial class AddPlatformProfiles : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // SQLite requires table rebuilds for new foreign keys. Disable enforcement outside the
        // migration transaction so dependent history/event rows survive those rebuilds unchanged.
        migrationBuilder.Sql("PRAGMA foreign_keys = OFF;", suppressTransaction: true);

        migrationBuilder.CreateTable(
            name: "PlatformProfiles",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                PlatformId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false,
                    collation: "NOCASE"),
                DisplayName = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false,
                    collation: "NOCASE"),
                ConfigurationDirectory = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false,
                    collation: "NOCASE"),
                Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                IsDefault = table.Column<bool>(type: "INTEGER", nullable: false),
                ShowUsageInStatusBar = table.Column<bool>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PlatformProfiles", x => x.Id);
                table.UniqueConstraint("AK_PlatformProfiles_Id_PlatformId", x => new { x.Id, x.PlatformId });
                table.CheckConstraint("CK_PlatformProfiles_DefaultName",
                    "(IsDefault = 1 AND lower(Name) = 'default') OR (IsDefault = 0 AND lower(Name) <> 'default')");
                table.ForeignKey(
                    name: "FK_PlatformProfiles_Platforms_PlatformId",
                    column: x => x.PlatformId,
                    principalTable: "Platforms",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_PlatformProfiles_PlatformId",
            table: "PlatformProfiles",
            column: "PlatformId",
            unique: true,
            filter: "\"IsDefault\" = 1");

        migrationBuilder.CreateIndex(
            name: "IX_PlatformProfiles_PlatformId_ConfigurationDirectory",
            table: "PlatformProfiles",
            columns: new[] { "PlatformId", "ConfigurationDirectory" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_PlatformProfiles_PlatformId_DisplayName",
            table: "PlatformProfiles",
            columns: new[] { "PlatformId", "DisplayName" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_PlatformProfiles_PlatformId_Name",
            table: "PlatformProfiles",
            columns: new[] { "PlatformId", "Name" },
            unique: true);

        var userProfile = Environment.GetEnvironmentVariable("USERPROFILE");
        if (string.IsNullOrWhiteSpace(userProfile))
            userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(userProfile))
            throw new InvalidOperationException("Das Windows-Benutzerprofil konnte für die Standardprofile nicht aufgelöst werden.");
        var codexDirectory = SqlLiteral(Path.Combine(userProfile, ".codex"));
        var claudeDirectory = SqlLiteral(Path.Combine(userProfile, ".claude"));
        var fallbackPrefix = SqlLiteral(Path.TrimEndingDirectorySeparator(Path.GetFullPath(userProfile))
            + Path.DirectorySeparatorChar + ".");

        migrationBuilder.Sql($$"""
            INSERT INTO "PlatformProfiles"
                ("Id", "PlatformId", "Name", "DisplayName", "ConfigurationDirectory",
                 "Enabled", "IsDefault", "ShowUsageInStatusBar")
            SELECT
                lower(hex(randomblob(4)) || '-' || hex(randomblob(2)) || '-' || hex(randomblob(2)) || '-' ||
                      hex(randomblob(2)) || '-' || hex(randomblob(6))),
                "Id",
                'default',
                'Standard',
                CASE lower("Id")
                    WHEN 'codex' THEN '{{codexDirectory}}'
                    WHEN 'claude' THEN '{{claudeDirectory}}'
                    ELSE '{{fallbackPrefix}}' || "Id"
                END,
                1,
                1,
                "ShowUsageInStatusBar"
            FROM "Platforms";
            """);

        migrationBuilder.Sql("""
            CREATE TABLE "__temp_WorkItems" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_WorkItems" PRIMARY KEY,
                "Title" TEXT NOT NULL,
                "Priority" INTEGER NOT NULL,
                "PlatformId" TEXT NOT NULL,
                "PlatformProfileId" TEXT NOT NULL,
                "ModelId" TEXT NOT NULL,
                "Effort" TEXT NOT NULL,
                "PromptPath" TEXT NOT NULL,
                "AutoCommit" INTEGER NOT NULL,
                "CommitMessage" TEXT NULL,
                "ProjectId" TEXT NULL,
                "CreatedAtUtc" TEXT NOT NULL,
                "FirstAttemptStartedAtUtc" TEXT NULL,
                "HasExecutionStarted" INTEGER NOT NULL,
                "Status" INTEGER NOT NULL,
                "NormalRetryCount" INTEGER NOT NULL,
                CONSTRAINT "CK_WorkItems_NormalRetryCount" CHECK (NormalRetryCount >= 0),
                CONSTRAINT "CK_WorkItems_Priority" CHECK (Priority >= 0 AND Priority <= 100),
                CONSTRAINT "FK_WorkItems_Projects_ProjectId" FOREIGN KEY ("ProjectId") REFERENCES "Projects" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_WorkItems_PlatformProfiles_PlatformProfileId_PlatformId"
                    FOREIGN KEY ("PlatformProfileId", "PlatformId")
                    REFERENCES "PlatformProfiles" ("Id", "PlatformId") ON DELETE RESTRICT
            );

            INSERT INTO "__temp_WorkItems"
                ("Id", "Title", "Priority", "PlatformId", "PlatformProfileId", "ModelId", "Effort",
                 "PromptPath", "AutoCommit", "CommitMessage", "ProjectId", "CreatedAtUtc",
                 "FirstAttemptStartedAtUtc", "HasExecutionStarted", "Status", "NormalRetryCount")
            SELECT "Id", "Title", "Priority", "PlatformId",
                   (SELECT "Id" FROM "PlatformProfiles" AS p
                    WHERE p."PlatformId" = "WorkItems"."PlatformId" AND p."IsDefault" = 1),
                   "ModelId", "Effort", "PromptPath", "AutoCommit", "CommitMessage", "ProjectId",
                   "CreatedAtUtc", "FirstAttemptStartedAtUtc", "HasExecutionStarted", "Status", "NormalRetryCount"
            FROM "WorkItems";

            DROP TABLE "WorkItems";
            ALTER TABLE "__temp_WorkItems" RENAME TO "WorkItems";
            CREATE INDEX "IX_WorkItems_ProjectId" ON "WorkItems" ("ProjectId");
            CREATE INDEX "IX_WorkItems_Status_Priority_CreatedAtUtc" ON "WorkItems" ("Status", "Priority", "CreatedAtUtc");
            CREATE INDEX "IX_WorkItems_PlatformProfileId_PlatformId" ON "WorkItems" ("PlatformProfileId", "PlatformId");

            CREATE TABLE "__temp_ExecutionAttempts" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_ExecutionAttempts" PRIMARY KEY,
                "WorkItemId" TEXT NOT NULL,
                "SequenceNumber" INTEGER NOT NULL,
                "PlatformId" TEXT NOT NULL,
                "PlatformProfileId" TEXT NOT NULL,
                "ModelId" TEXT NOT NULL,
                "Effort" TEXT NOT NULL,
                "StartedAtUtc" TEXT NOT NULL,
                "CompletedAtUtc" TEXT NOT NULL,
                "Result" INTEGER NOT NULL,
                "ExitCode" INTEGER NULL,
                "SessionId" TEXT NULL,
                "Diagnostic" TEXT NULL,
                CONSTRAINT "CK_ExecutionAttempts_SequenceNumber" CHECK (SequenceNumber > 0),
                CONSTRAINT "FK_ExecutionAttempts_WorkItems_WorkItemId"
                    FOREIGN KEY ("WorkItemId") REFERENCES "WorkItems" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_ExecutionAttempts_PlatformProfiles_PlatformProfileId_PlatformId"
                    FOREIGN KEY ("PlatformProfileId", "PlatformId")
                    REFERENCES "PlatformProfiles" ("Id", "PlatformId") ON DELETE RESTRICT
            );

            INSERT INTO "__temp_ExecutionAttempts"
                ("Id", "WorkItemId", "SequenceNumber", "PlatformId", "PlatformProfileId", "ModelId", "Effort",
                 "StartedAtUtc", "CompletedAtUtc", "Result", "ExitCode", "SessionId", "Diagnostic")
            SELECT "Id", "WorkItemId", "SequenceNumber", "PlatformId",
                   (SELECT "Id" FROM "PlatformProfiles" AS p
                    WHERE p."PlatformId" = "ExecutionAttempts"."PlatformId" AND p."IsDefault" = 1),
                   "ModelId", "Effort", "StartedAtUtc", "CompletedAtUtc", "Result", "ExitCode", "SessionId", "Diagnostic"
            FROM "ExecutionAttempts";

            DROP TABLE "ExecutionAttempts";
            ALTER TABLE "__temp_ExecutionAttempts" RENAME TO "ExecutionAttempts";
            CREATE UNIQUE INDEX "IX_ExecutionAttempts_WorkItemId_SequenceNumber"
                ON "ExecutionAttempts" ("WorkItemId", "SequenceNumber");
            CREATE INDEX "IX_ExecutionAttempts_PlatformProfileId_PlatformId"
                ON "ExecutionAttempts" ("PlatformProfileId", "PlatformId");
            """);

        migrationBuilder.Sql("PRAGMA foreign_keys = ON;", suppressTransaction: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("PRAGMA foreign_keys = OFF;", suppressTransaction: true);
        migrationBuilder.Sql("""
            CREATE TABLE "__temp_ExecutionAttempts" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_ExecutionAttempts" PRIMARY KEY,
                "WorkItemId" TEXT NOT NULL,
                "SequenceNumber" INTEGER NOT NULL,
                "PlatformId" TEXT NOT NULL,
                "ModelId" TEXT NOT NULL,
                "Effort" TEXT NOT NULL,
                "StartedAtUtc" TEXT NOT NULL,
                "CompletedAtUtc" TEXT NOT NULL,
                "Result" INTEGER NOT NULL,
                "ExitCode" INTEGER NULL,
                "SessionId" TEXT NULL,
                "Diagnostic" TEXT NULL,
                CONSTRAINT "CK_ExecutionAttempts_SequenceNumber" CHECK (SequenceNumber > 0),
                CONSTRAINT "FK_ExecutionAttempts_WorkItems_WorkItemId"
                    FOREIGN KEY ("WorkItemId") REFERENCES "WorkItems" ("Id") ON DELETE CASCADE
            );
            INSERT INTO "__temp_ExecutionAttempts"
                ("Id", "WorkItemId", "SequenceNumber", "PlatformId", "ModelId", "Effort",
                 "StartedAtUtc", "CompletedAtUtc", "Result", "ExitCode", "SessionId", "Diagnostic")
            SELECT "Id", "WorkItemId", "SequenceNumber", "PlatformId", "ModelId", "Effort",
                   "StartedAtUtc", "CompletedAtUtc", "Result", "ExitCode", "SessionId", "Diagnostic"
            FROM "ExecutionAttempts";
            DROP TABLE "ExecutionAttempts";
            ALTER TABLE "__temp_ExecutionAttempts" RENAME TO "ExecutionAttempts";
            CREATE UNIQUE INDEX "IX_ExecutionAttempts_WorkItemId_SequenceNumber"
                ON "ExecutionAttempts" ("WorkItemId", "SequenceNumber");

            CREATE TABLE "__temp_WorkItems" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_WorkItems" PRIMARY KEY,
                "Title" TEXT NOT NULL,
                "Priority" INTEGER NOT NULL,
                "PlatformId" TEXT NOT NULL,
                "ModelId" TEXT NOT NULL,
                "Effort" TEXT NOT NULL,
                "PromptPath" TEXT NOT NULL,
                "AutoCommit" INTEGER NOT NULL,
                "CommitMessage" TEXT NULL,
                "ProjectId" TEXT NULL,
                "CreatedAtUtc" TEXT NOT NULL,
                "FirstAttemptStartedAtUtc" TEXT NULL,
                "HasExecutionStarted" INTEGER NOT NULL,
                "Status" INTEGER NOT NULL,
                "NormalRetryCount" INTEGER NOT NULL,
                CONSTRAINT "CK_WorkItems_NormalRetryCount" CHECK (NormalRetryCount >= 0),
                CONSTRAINT "CK_WorkItems_Priority" CHECK (Priority >= 0 AND Priority <= 100),
                CONSTRAINT "FK_WorkItems_Projects_ProjectId" FOREIGN KEY ("ProjectId") REFERENCES "Projects" ("Id") ON DELETE RESTRICT
            );
            INSERT INTO "__temp_WorkItems"
                ("Id", "Title", "Priority", "PlatformId", "ModelId", "Effort", "PromptPath",
                 "AutoCommit", "CommitMessage", "ProjectId", "CreatedAtUtc", "FirstAttemptStartedAtUtc",
                 "HasExecutionStarted", "Status", "NormalRetryCount")
            SELECT "Id", "Title", "Priority", "PlatformId", "ModelId", "Effort", "PromptPath",
                   "AutoCommit", "CommitMessage", "ProjectId", "CreatedAtUtc", "FirstAttemptStartedAtUtc",
                   "HasExecutionStarted", "Status", "NormalRetryCount"
            FROM "WorkItems";
            DROP TABLE "WorkItems";
            ALTER TABLE "__temp_WorkItems" RENAME TO "WorkItems";
            CREATE INDEX "IX_WorkItems_ProjectId" ON "WorkItems" ("ProjectId");
            CREATE INDEX "IX_WorkItems_Status_Priority_CreatedAtUtc" ON "WorkItems" ("Status", "Priority", "CreatedAtUtc");
            """);
        migrationBuilder.DropTable(name: "PlatformProfiles");
        migrationBuilder.Sql("PRAGMA foreign_keys = ON;", suppressTransaction: true);
    }

    private static string SqlLiteral(string value) => value.Replace("'", "''", StringComparison.Ordinal);
}
