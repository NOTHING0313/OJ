using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnlineJudge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFileSubmissionReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReviewComment",
                table: "ChallengeTaskFileSubmissions",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReviewScore",
                table: "ChallengeTaskFileSubmissions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReviewedAt",
                table: "ChallengeTaskFileSubmissions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewedByUserId",
                table: "ChallengeTaskFileSubmissions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChallengeTaskFileSubmissions_ReviewedByUserId",
                table: "ChallengeTaskFileSubmissions",
                column: "ReviewedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ChallengeTaskFileSubmissions_Users_ReviewedByUserId",
                table: "ChallengeTaskFileSubmissions",
                column: "ReviewedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChallengeTaskFileSubmissions_Users_ReviewedByUserId",
                table: "ChallengeTaskFileSubmissions");

            migrationBuilder.DropIndex(
                name: "IX_ChallengeTaskFileSubmissions_ReviewedByUserId",
                table: "ChallengeTaskFileSubmissions");

            migrationBuilder.DropColumn(
                name: "ReviewComment",
                table: "ChallengeTaskFileSubmissions");

            migrationBuilder.DropColumn(
                name: "ReviewScore",
                table: "ChallengeTaskFileSubmissions");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "ChallengeTaskFileSubmissions");

            migrationBuilder.DropColumn(
                name: "ReviewedByUserId",
                table: "ChallengeTaskFileSubmissions");
        }
    }
}
