using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnlineJudge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFunctionJudgeMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ArgumentsJson",
                table: "TestCases",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExpectedJson",
                table: "TestCases",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FunctionSpecJson",
                table: "Problems",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "JudgeMode",
                table: "Problems",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "StarterCodeJson",
                table: "Problems",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArgumentsJson",
                table: "TestCases");

            migrationBuilder.DropColumn(
                name: "ExpectedJson",
                table: "TestCases");

            migrationBuilder.DropColumn(
                name: "FunctionSpecJson",
                table: "Problems");

            migrationBuilder.DropColumn(
                name: "JudgeMode",
                table: "Problems");

            migrationBuilder.DropColumn(
                name: "StarterCodeJson",
                table: "Problems");
        }
    }
}
