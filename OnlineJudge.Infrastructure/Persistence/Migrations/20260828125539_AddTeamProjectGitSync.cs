using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnlineJudge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamProjectGitSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DefaultBranch",
                table: "TeamProjects",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastSyncAttemptAt",
                table: "TeamProjects",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastSyncError",
                table: "TeamProjects",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LastSyncStatus",
                table: "TeamProjects",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastSyncedAt",
                table: "TeamProjects",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DefaultBranch",
                table: "TeamProjects");

            migrationBuilder.DropColumn(
                name: "LastSyncAttemptAt",
                table: "TeamProjects");

            migrationBuilder.DropColumn(
                name: "LastSyncError",
                table: "TeamProjects");

            migrationBuilder.DropColumn(
                name: "LastSyncStatus",
                table: "TeamProjects");

            migrationBuilder.DropColumn(
                name: "LastSyncedAt",
                table: "TeamProjects");
        }
    }
}
