using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KIScheduler.Infrastructure.Persistence.Migrations;

[DbContext(typeof(KischedulerDbContext))]
[Migration("20260914170000_AllowHistoricalProfileIdentityReuse")]
public partial class AllowHistoricalProfileIdentityReuse : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex("IX_PlatformProfiles_PlatformId", "PlatformProfiles");
        migrationBuilder.DropIndex("IX_PlatformProfiles_PlatformId_ConfigurationDirectory", "PlatformProfiles");
        migrationBuilder.DropIndex("IX_PlatformProfiles_PlatformId_DisplayName", "PlatformProfiles");
        migrationBuilder.DropIndex("IX_PlatformProfiles_PlatformId_Name", "PlatformProfiles");

        migrationBuilder.CreateIndex("IX_PlatformProfiles_PlatformId", "PlatformProfiles", "PlatformId",
            unique: true, filter: "\"IsDefault\" = 1 AND \"Enabled\" = 1");
        migrationBuilder.CreateIndex("IX_PlatformProfiles_PlatformId_ConfigurationDirectory", "PlatformProfiles",
            new[] { "PlatformId", "ConfigurationDirectory" }, unique: true, filter: "\"Enabled\" = 1");
        migrationBuilder.CreateIndex("IX_PlatformProfiles_PlatformId_DisplayName", "PlatformProfiles",
            new[] { "PlatformId", "DisplayName" }, unique: true, filter: "\"Enabled\" = 1");
        migrationBuilder.CreateIndex("IX_PlatformProfiles_PlatformId_Name", "PlatformProfiles",
            new[] { "PlatformId", "Name" }, unique: true, filter: "\"Enabled\" = 1");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex("IX_PlatformProfiles_PlatformId", "PlatformProfiles");
        migrationBuilder.DropIndex("IX_PlatformProfiles_PlatformId_ConfigurationDirectory", "PlatformProfiles");
        migrationBuilder.DropIndex("IX_PlatformProfiles_PlatformId_DisplayName", "PlatformProfiles");
        migrationBuilder.DropIndex("IX_PlatformProfiles_PlatformId_Name", "PlatformProfiles");

        migrationBuilder.CreateIndex("IX_PlatformProfiles_PlatformId", "PlatformProfiles", "PlatformId",
            unique: true, filter: "\"IsDefault\" = 1");
        migrationBuilder.CreateIndex("IX_PlatformProfiles_PlatformId_ConfigurationDirectory", "PlatformProfiles",
            new[] { "PlatformId", "ConfigurationDirectory" }, unique: true);
        migrationBuilder.CreateIndex("IX_PlatformProfiles_PlatformId_DisplayName", "PlatformProfiles",
            new[] { "PlatformId", "DisplayName" }, unique: true);
        migrationBuilder.CreateIndex("IX_PlatformProfiles_PlatformId_Name", "PlatformProfiles",
            new[] { "PlatformId", "Name" }, unique: true);
    }
}
