using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnlineJudge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamChallenges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ChallengeTeamParticipantId",
                table: "Submissions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ParticipationMode",
                table: "Challenges",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "ChallengeTeamParticipants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChallengeId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamNameSnapshot = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    RegisteredByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RegisteredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChallengeTeamParticipants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChallengeTeamParticipants_Challenges_ChallengeId",
                        column: x => x.ChallengeId,
                        principalTable: "Challenges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChallengeTeamParticipants_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChallengeTeamParticipants_Users_RegisteredByUserId",
                        column: x => x.RegisteredByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ChallengeTeamRosterMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChallengeTeamParticipantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChallengeId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserNameSnapshot = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TeamMemberRoleSnapshot = table.Column<int>(type: "integer", nullable: false),
                    RegisteredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChallengeTeamRosterMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChallengeTeamRosterMembers_ChallengeTeamParticipants_Challe~",
                        column: x => x.ChallengeTeamParticipantId,
                        principalTable: "ChallengeTeamParticipants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChallengeTeamRosterMembers_Challenges_ChallengeId",
                        column: x => x.ChallengeId,
                        principalTable: "Challenges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChallengeTeamRosterMembers_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChallengeTeamRosterMembers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ChallengeTeamTaskCompletions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChallengeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChallengeTaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChallengeTeamParticipantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BestSubmissionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ContributorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Score = table.Column<int>(type: "integer", nullable: false),
                    IsCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChallengeTeamTaskCompletions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChallengeTeamTaskCompletions_ChallengeTasks_ChallengeTaskId",
                        column: x => x.ChallengeTaskId,
                        principalTable: "ChallengeTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChallengeTeamTaskCompletions_ChallengeTeamParticipants_Chal~",
                        column: x => x.ChallengeTeamParticipantId,
                        principalTable: "ChallengeTeamParticipants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChallengeTeamTaskCompletions_Challenges_ChallengeId",
                        column: x => x.ChallengeId,
                        principalTable: "Challenges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChallengeTeamTaskCompletions_Submissions_BestSubmissionId",
                        column: x => x.BestSubmissionId,
                        principalTable: "Submissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ChallengeTeamTaskCompletions_Users_ContributorUserId",
                        column: x => x.ContributorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_ChallengeTeamParticipantId",
                table: "Submissions",
                column: "ChallengeTeamParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_ChallengeTeamParticipants_ChallengeId_TeamId",
                table: "ChallengeTeamParticipants",
                columns: new[] { "ChallengeId", "TeamId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChallengeTeamParticipants_RegisteredByUserId",
                table: "ChallengeTeamParticipants",
                column: "RegisteredByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ChallengeTeamParticipants_TeamId",
                table: "ChallengeTeamParticipants",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_ChallengeTeamRosterMembers_ChallengeId_UserId",
                table: "ChallengeTeamRosterMembers",
                columns: new[] { "ChallengeId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChallengeTeamRosterMembers_ChallengeTeamParticipantId_UserId",
                table: "ChallengeTeamRosterMembers",
                columns: new[] { "ChallengeTeamParticipantId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChallengeTeamRosterMembers_TeamId",
                table: "ChallengeTeamRosterMembers",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_ChallengeTeamRosterMembers_UserId",
                table: "ChallengeTeamRosterMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ChallengeTeamTaskCompletions_BestSubmissionId",
                table: "ChallengeTeamTaskCompletions",
                column: "BestSubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_ChallengeTeamTaskCompletions_ChallengeId",
                table: "ChallengeTeamTaskCompletions",
                column: "ChallengeId");

            migrationBuilder.CreateIndex(
                name: "IX_ChallengeTeamTaskCompletions_ChallengeTaskId",
                table: "ChallengeTeamTaskCompletions",
                column: "ChallengeTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_ChallengeTeamTaskCompletions_ChallengeTeamParticipantId_Cha~",
                table: "ChallengeTeamTaskCompletions",
                columns: new[] { "ChallengeTeamParticipantId", "ChallengeTaskId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChallengeTeamTaskCompletions_ContributorUserId",
                table: "ChallengeTeamTaskCompletions",
                column: "ContributorUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Submissions_ChallengeTeamParticipants_ChallengeTeamParticip~",
                table: "Submissions",
                column: "ChallengeTeamParticipantId",
                principalTable: "ChallengeTeamParticipants",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Submissions_ChallengeTeamParticipants_ChallengeTeamParticip~",
                table: "Submissions");

            migrationBuilder.DropTable(
                name: "ChallengeTeamRosterMembers");

            migrationBuilder.DropTable(
                name: "ChallengeTeamTaskCompletions");

            migrationBuilder.DropTable(
                name: "ChallengeTeamParticipants");

            migrationBuilder.DropIndex(
                name: "IX_Submissions_ChallengeTeamParticipantId",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "ChallengeTeamParticipantId",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "ParticipationMode",
                table: "Challenges");
        }
    }
}
