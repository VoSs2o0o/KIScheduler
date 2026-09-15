using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KIScheduler.Infrastructure.Persistence.Migrations;

[DbContext(typeof(KischedulerDbContext))]
[Migration("20260916100000_RenameDefaultProfileDisplayNames")]
public partial class RenameDefaultProfileDisplayNames : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE "PlatformProfiles"
            SET "DisplayName" = CASE lower("PlatformId")
                WHEN 'claude' THEN 'ClaudeStd'
                WHEN 'codex' THEN 'CodexStd'
            END
            WHERE lower("PlatformId") IN ('claude', 'codex')
              AND lower("Name") = 'default'
              AND "DisplayName" = 'Standard'
              AND NOT EXISTS (
                  SELECT 1 FROM "PlatformProfiles" AS other
                  WHERE lower(other."PlatformId") = lower("PlatformProfiles"."PlatformId")
                    AND other."Enabled" = 1
                    AND lower(other."DisplayName") = CASE lower("PlatformProfiles"."PlatformId")
                        WHEN 'claude' THEN 'claudestd'
                        WHEN 'codex' THEN 'codexstd'
                    END
              );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE "PlatformProfiles"
            SET "DisplayName" = 'Standard'
            WHERE lower("PlatformId") IN ('claude', 'codex')
              AND lower("Name") = 'default'
              AND lower("DisplayName") = CASE lower("PlatformId")
                  WHEN 'claude' THEN 'claudestd'
                  WHEN 'codex' THEN 'codexstd'
              END;
            """);
    }
}
