using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnlineJudge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddChallengePeerReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProjectNameSnapshot",
                table: "ChallengeTeamParticipants",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepositoryUrlSnapshot",
                table: "ChallengeTeamParticipants",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SelectedTeamProjectId",
                table: "ChallengeTeamParticipants",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PeerReviewEnabled",
                table: "Challenges",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PeerReviewEndAt",
                table: "Challenges",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ChallengePeerReviewAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChallengeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewerParticipantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetParticipantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewerTeamNameSnapshot = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    TargetTeamNameSnapshot = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    TargetProjectNameSnapshot = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    TargetRepositoryUrlSnapshot = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChallengePeerReviewAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChallengePeerReviewAssignments_ChallengeTeamParticipants_Re~",
                        column: x => x.ReviewerParticipantId,
                        principalTable: "ChallengeTeamParticipants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChallengePeerReviewAssignments_ChallengeTeamParticipants_Ta~",
                        column: x => x.TargetParticipantId,
                        principalTable: "ChallengeTeamParticipants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChallengePeerReviewAssignments_Challenges_ChallengeId",
                        column: x => x.ChallengeId,
                        principalTable: "Challenges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChallengePeerReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChallengeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewerParticipantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetParticipantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    OverallScore = table.Column<int>(type: "integer", nullable: true),
                    Summary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Strengths = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Improvements = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChallengePeerReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChallengePeerReviews_ChallengePeerReviewAssignments_Assignm~",
                        column: x => x.AssignmentId,
                        principalTable: "ChallengePeerReviewAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChallengePeerReviews_ChallengeTeamParticipants_ReviewerPart~",
                        column: x => x.ReviewerParticipantId,
                        principalTable: "ChallengeTeamParticipants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChallengePeerReviews_ChallengeTeamParticipants_TargetPartic~",
                        column: x => x.TargetParticipantId,
                        principalTable: "ChallengeTeamParticipants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChallengePeerReviews_Challenges_ChallengeId",
                        column: x => x.ChallengeId,
                        principalTable: "Challenges",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChallengeTeamParticipants_SelectedTeamProjectId",
                table: "ChallengeTeamParticipants",
                column: "SelectedTeamProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ChallengePeerReviewAssignments_ChallengeId_ReviewerParticip~",
                table: "ChallengePeerReviewAssignments",
                columns: new[] { "ChallengeId", "ReviewerParticipantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChallengePeerReviewAssignments_ChallengeId_TargetParticipan~",
                table: "ChallengePeerReviewAssignments",
                columns: new[] { "ChallengeId", "TargetParticipantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChallengePeerReviewAssignments_ReviewerParticipantId",
                table: "ChallengePeerReviewAssignments",
                column: "ReviewerParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_ChallengePeerReviewAssignments_TargetParticipantId",
                table: "ChallengePeerReviewAssignments",
                column: "TargetParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_ChallengePeerReviews_AssignmentId",
                table: "ChallengePeerReviews",
                column: "AssignmentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChallengePeerReviews_ChallengeId",
                table: "ChallengePeerReviews",
                column: "ChallengeId");

            migrationBuilder.CreateIndex(
                name: "IX_ChallengePeerReviews_ReviewerParticipantId",
                table: "ChallengePeerReviews",
                column: "ReviewerParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_ChallengePeerReviews_TargetParticipantId",
                table: "ChallengePeerReviews",
                column: "TargetParticipantId");

            migrationBuilder.AddForeignKey(
                name: "FK_ChallengeTeamParticipants_TeamProjects_SelectedTeamProjectId",
                table: "ChallengeTeamParticipants",
                column: "SelectedTeamProjectId",
                principalTable: "TeamProjects",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChallengeTeamParticipants_TeamProjects_SelectedTeamProjectId",
                table: "ChallengeTeamParticipants");

            migrationBuilder.DropTable(
                name: "ChallengePeerReviews");

            migrationBuilder.DropTable(
                name: "ChallengePeerReviewAssignments");

            migrationBuilder.DropIndex(
                name: "IX_ChallengeTeamParticipants_SelectedTeamProjectId",
                table: "ChallengeTeamParticipants");

            migrationBuilder.DropColumn(
                name: "ProjectNameSnapshot",
                table: "ChallengeTeamParticipants");

            migrationBuilder.DropColumn(
                name: "RepositoryUrlSnapshot",
                table: "ChallengeTeamParticipants");

            migrationBuilder.DropColumn(
                name: "SelectedTeamProjectId",
                table: "ChallengeTeamParticipants");

            migrationBuilder.DropColumn(
                name: "PeerReviewEnabled",
                table: "Challenges");

            migrationBuilder.DropColumn(
                name: "PeerReviewEndAt",
                table: "Challenges");
        }
    }
}
