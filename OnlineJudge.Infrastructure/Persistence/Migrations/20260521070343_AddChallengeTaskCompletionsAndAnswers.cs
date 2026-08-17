using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnlineJudge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddChallengeTaskCompletionsAndAnswers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ChallengeTaskId",
                table: "Submissions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ChallengeTaskAnswers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChallengeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChallengeTaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChallengeTaskAnswers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChallengeTaskAnswers_ChallengeTasks_ChallengeTaskId",
                        column: x => x.ChallengeTaskId,
                        principalTable: "ChallengeTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChallengeTaskAnswers_Challenges_ChallengeId",
                        column: x => x.ChallengeId,
                        principalTable: "Challenges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChallengeTaskAnswers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ChallengeTaskCompletions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChallengeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChallengeTaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChallengeTaskCompletions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChallengeTaskCompletions_ChallengeTasks_ChallengeTaskId",
                        column: x => x.ChallengeTaskId,
                        principalTable: "ChallengeTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChallengeTaskCompletions_Challenges_ChallengeId",
                        column: x => x.ChallengeId,
                        principalTable: "Challenges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChallengeTaskCompletions_Submissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "Submissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ChallengeTaskCompletions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_ChallengeTaskId",
                table: "Submissions",
                column: "ChallengeTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_ChallengeTaskAnswers_ChallengeId",
                table: "ChallengeTaskAnswers",
                column: "ChallengeId");

            migrationBuilder.CreateIndex(
                name: "IX_ChallengeTaskAnswers_ChallengeTaskId",
                table: "ChallengeTaskAnswers",
                column: "ChallengeTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_ChallengeTaskAnswers_UserId_ChallengeTaskId",
                table: "ChallengeTaskAnswers",
                columns: new[] { "UserId", "ChallengeTaskId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChallengeTaskCompletions_ChallengeId",
                table: "ChallengeTaskCompletions",
                column: "ChallengeId");

            migrationBuilder.CreateIndex(
                name: "IX_ChallengeTaskCompletions_ChallengeTaskId",
                table: "ChallengeTaskCompletions",
                column: "ChallengeTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_ChallengeTaskCompletions_SubmissionId",
                table: "ChallengeTaskCompletions",
                column: "SubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_ChallengeTaskCompletions_UserId_ChallengeTaskId",
                table: "ChallengeTaskCompletions",
                columns: new[] { "UserId", "ChallengeTaskId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Submissions_ChallengeTasks_ChallengeTaskId",
                table: "Submissions",
                column: "ChallengeTaskId",
                principalTable: "ChallengeTasks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Submissions_ChallengeTasks_ChallengeTaskId",
                table: "Submissions");

            migrationBuilder.DropTable(
                name: "ChallengeTaskAnswers");

            migrationBuilder.DropTable(
                name: "ChallengeTaskCompletions");

            migrationBuilder.DropIndex(
                name: "IX_Submissions_ChallengeTaskId",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "ChallengeTaskId",
                table: "Submissions");
        }
    }
}
