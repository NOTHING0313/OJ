using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnlineJudge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SetSubmissionChallengeTaskDeleteBehavior : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Submissions_ChallengeTasks_ChallengeTaskId",
                table: "Submissions");

            migrationBuilder.AddForeignKey(
                name: "FK_Submissions_ChallengeTasks_ChallengeTaskId",
                table: "Submissions",
                column: "ChallengeTaskId",
                principalTable: "ChallengeTasks",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Submissions_ChallengeTasks_ChallengeTaskId",
                table: "Submissions");

            migrationBuilder.AddForeignKey(
                name: "FK_Submissions_ChallengeTasks_ChallengeTaskId",
                table: "Submissions",
                column: "ChallengeTaskId",
                principalTable: "ChallengeTasks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
