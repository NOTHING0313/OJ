using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnlineJudge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamChat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TeamChatMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    SenderUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    RelatedChallengeId = table.Column<Guid>(type: "uuid", nullable: true),
                    RelatedPeerReviewAssignmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    EventKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamChatMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeamChatMessages_ChallengePeerReviewAssignments_RelatedPeer~",
                        column: x => x.RelatedPeerReviewAssignmentId,
                        principalTable: "ChallengePeerReviewAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TeamChatMessages_Challenges_RelatedChallengeId",
                        column: x => x.RelatedChallengeId,
                        principalTable: "Challenges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TeamChatMessages_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeamChatMessages_Users_SenderUserId",
                        column: x => x.SenderUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TeamChatMessages_EventKey",
                table: "TeamChatMessages",
                column: "EventKey",
                unique: true,
                filter: "\"EventKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TeamChatMessages_RelatedChallengeId",
                table: "TeamChatMessages",
                column: "RelatedChallengeId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamChatMessages_RelatedPeerReviewAssignmentId",
                table: "TeamChatMessages",
                column: "RelatedPeerReviewAssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamChatMessages_SenderUserId",
                table: "TeamChatMessages",
                column: "SenderUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamChatMessages_TeamId_CreatedAt_Id",
                table: "TeamChatMessages",
                columns: new[] { "TeamId", "CreatedAt", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TeamChatMessages");
        }
    }
}
