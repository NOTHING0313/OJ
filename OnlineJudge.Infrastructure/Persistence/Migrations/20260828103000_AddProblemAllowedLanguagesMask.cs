using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OnlineJudge.Infrastructure.Persistence;

#nullable disable

namespace OnlineJudge.Infrastructure.Persistence.Migrations;

[DbContext(typeof(OnlineJudgeDbContext))]
[Migration("20260828103000_AddProblemAllowedLanguagesMask")]
public class AddProblemAllowedLanguagesMask : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "AllowedLanguagesMask",
            table: "Problems",
            type: "integer",
            nullable: false,
            defaultValue: 0);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "AllowedLanguagesMask", table: "Problems");
    }
}