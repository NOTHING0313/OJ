using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnlineJudge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEditableTestCasesAndResultSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "TestCases",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "TestCases",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "TestCases",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql("UPDATE \"TestCases\" SET \"UpdatedAt\" = \"CreatedAt\"");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "TestCases",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExpectedJsonSnapshot",
                table: "SubmissionCaseResults",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExpectedOutputSnapshot",
                table: "SubmissionCaseResults",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ScoreSnapshot",
                table: "SubmissionCaseResults",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VisibilitySnapshot",
                table: "SubmissionCaseResults",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "TestCases");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "TestCases");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "TestCases");

            migrationBuilder.DropColumn(
                name: "ExpectedJsonSnapshot",
                table: "SubmissionCaseResults");

            migrationBuilder.DropColumn(
                name: "ExpectedOutputSnapshot",
                table: "SubmissionCaseResults");

            migrationBuilder.DropColumn(
                name: "ScoreSnapshot",
                table: "SubmissionCaseResults");

            migrationBuilder.DropColumn(
                name: "VisibilitySnapshot",
                table: "SubmissionCaseResults");
        }
    }
}
