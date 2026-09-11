using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KIScheduler.Infrastructure.Persistence.Migrations;

[DbContext(typeof(KischedulerDbContext))]
[Migration("20260911141500_AddProjectDefaultTemplate")]
public partial class AddProjectDefaultTemplate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "DefaultTemplate",
            table: "Projects",
            type: "TEXT",
            maxLength: 255,
            nullable: false,
            defaultValue: "classlib");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "DefaultTemplate",
            table: "Projects");
    }
}
