using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KIScheduler.Infrastructure.Persistence.Migrations;

[DbContext(typeof(KischedulerDbContext))]
[Migration("20260915120000_AllowStableProfileNames")]
public partial class AllowStableProfileNames : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("PRAGMA foreign_keys = OFF;", suppressTransaction: true);
        migrationBuilder.Sql("""
            CREATE TABLE "__temp_PlatformProfiles" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_PlatformProfiles" PRIMARY KEY,
                "PlatformId" TEXT NOT NULL,
                "Name" TEXT NOT NULL COLLATE NOCASE,
                "DisplayName" TEXT NOT NULL COLLATE NOCASE,
                "ConfigurationDirectory" TEXT NOT NULL COLLATE NOCASE,
                "Enabled" INTEGER NOT NULL,
                "IsDefault" INTEGER NOT NULL,
                "ShowUsageInStatusBar" INTEGER NOT NULL,
                CONSTRAINT "AK_PlatformProfiles_Id_PlatformId" UNIQUE ("Id", "PlatformId"),
                CONSTRAINT "FK_PlatformProfiles_Platforms_PlatformId"
                    FOREIGN KEY ("PlatformId") REFERENCES "Platforms" ("Id") ON DELETE RESTRICT
            );

            INSERT INTO "__temp_PlatformProfiles"
                ("Id", "PlatformId", "Name", "DisplayName", "ConfigurationDirectory",
                 "Enabled", "IsDefault", "ShowUsageInStatusBar")
            SELECT "Id", "PlatformId", "Name", "DisplayName", "ConfigurationDirectory",
                   "Enabled", "IsDefault", "ShowUsageInStatusBar"
            FROM "PlatformProfiles";

            DROP TABLE "PlatformProfiles";
            ALTER TABLE "__temp_PlatformProfiles" RENAME TO "PlatformProfiles";

            CREATE UNIQUE INDEX "IX_PlatformProfiles_PlatformId"
                ON "PlatformProfiles" ("PlatformId")
                WHERE "IsDefault" = 1 AND "Enabled" = 1;
            CREATE UNIQUE INDEX "IX_PlatformProfiles_PlatformId_ConfigurationDirectory"
                ON "PlatformProfiles" ("PlatformId", "ConfigurationDirectory")
                WHERE "Enabled" = 1;
            CREATE UNIQUE INDEX "IX_PlatformProfiles_PlatformId_DisplayName"
                ON "PlatformProfiles" ("PlatformId", "DisplayName")
                WHERE "Enabled" = 1;
            CREATE UNIQUE INDEX "IX_PlatformProfiles_PlatformId_Name"
                ON "PlatformProfiles" ("PlatformId", "Name")
                WHERE "Enabled" = 1;
            """);
        migrationBuilder.Sql("PRAGMA foreign_keys = ON;", suppressTransaction: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("PRAGMA foreign_keys = OFF;", suppressTransaction: true);
        migrationBuilder.Sql("""
            CREATE TABLE "__temp_PlatformProfiles" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_PlatformProfiles" PRIMARY KEY,
                "PlatformId" TEXT NOT NULL,
                "Name" TEXT NOT NULL COLLATE NOCASE,
                "DisplayName" TEXT NOT NULL COLLATE NOCASE,
                "ConfigurationDirectory" TEXT NOT NULL COLLATE NOCASE,
                "Enabled" INTEGER NOT NULL,
                "IsDefault" INTEGER NOT NULL,
                "ShowUsageInStatusBar" INTEGER NOT NULL,
                CONSTRAINT "AK_PlatformProfiles_Id_PlatformId" UNIQUE ("Id", "PlatformId"),
                CONSTRAINT "CK_PlatformProfiles_DefaultName"
                    CHECK ((IsDefault = 1 AND lower(Name) = 'default') OR
                           (IsDefault = 0 AND lower(Name) <> 'default')),
                CONSTRAINT "FK_PlatformProfiles_Platforms_PlatformId"
                    FOREIGN KEY ("PlatformId") REFERENCES "Platforms" ("Id") ON DELETE RESTRICT
            );

            INSERT INTO "__temp_PlatformProfiles"
                ("Id", "PlatformId", "Name", "DisplayName", "ConfigurationDirectory",
                 "Enabled", "IsDefault", "ShowUsageInStatusBar")
            SELECT "Id", "PlatformId", "Name", "DisplayName", "ConfigurationDirectory",
                   "Enabled", "IsDefault", "ShowUsageInStatusBar"
            FROM "PlatformProfiles";

            DROP TABLE "PlatformProfiles";
            ALTER TABLE "__temp_PlatformProfiles" RENAME TO "PlatformProfiles";

            CREATE UNIQUE INDEX "IX_PlatformProfiles_PlatformId"
                ON "PlatformProfiles" ("PlatformId")
                WHERE "IsDefault" = 1 AND "Enabled" = 1;
            CREATE UNIQUE INDEX "IX_PlatformProfiles_PlatformId_ConfigurationDirectory"
                ON "PlatformProfiles" ("PlatformId", "ConfigurationDirectory")
                WHERE "Enabled" = 1;
            CREATE UNIQUE INDEX "IX_PlatformProfiles_PlatformId_DisplayName"
                ON "PlatformProfiles" ("PlatformId", "DisplayName")
                WHERE "Enabled" = 1;
            CREATE UNIQUE INDEX "IX_PlatformProfiles_PlatformId_Name"
                ON "PlatformProfiles" ("PlatformId", "Name")
                WHERE "Enabled" = 1;
            """);
        migrationBuilder.Sql("PRAGMA foreign_keys = ON;", suppressTransaction: true);
    }
}
