using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KIScheduler.Infrastructure.Persistence.Migrations;

[DbContext(typeof(KischedulerDbContext))]
[Migration("20260911170000_AddCodexUsageBucketMetadata")]
public partial class AddCodexUsageBucketMetadata : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "LimitId",
            table: "UsageWindows",
            type: "TEXT",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "LimitName",
            table: "UsageWindows",
            type: "TEXT",
            maxLength: 300,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "LimitId", table: "UsageWindows");
        migrationBuilder.DropColumn(name: "LimitName", table: "UsageWindows");
    }
}
