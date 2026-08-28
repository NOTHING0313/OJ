using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnlineJudge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLeaderboardSeasons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsLeaderboardAnonymous",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "LeaderboardSeasons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    StartAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FreezeAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PublicUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    IsCurrent = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaderboardSeasons", x => x.Id);
                    table.CheckConstraint("CK_LeaderboardSeasons_TimeOrder", "\"StartAt\" < \"FreezeAt\" AND \"FreezeAt\" < \"PublicUntil\"");
                    table.ForeignKey(
                        name: "FK_LeaderboardSeasons_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LeaderboardSeasonAliases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Alias = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaderboardSeasonAliases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeaderboardSeasonAliases_LeaderboardSeasons_SeasonId",
                        column: x => x.SeasonId,
                        principalTable: "LeaderboardSeasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LeaderboardSeasonAliases_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LeaderboardSeasonArchiveEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Alias = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: false),
                    DisplayNameSnapshot = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    WasAnonymous = table.Column<bool>(type: "boolean", nullable: false),
                    FinalRank = table.Column<int>(type: "integer", nullable: false),
                    FinalScore = table.Column<int>(type: "integer", nullable: false),
                    FinalBaseScore = table.Column<int>(type: "integer", nullable: false),
                    SolvedCount = table.Column<int>(type: "integer", nullable: false),
                    LastScoreImprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaderboardSeasonArchiveEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeaderboardSeasonArchiveEntries_LeaderboardSeasons_SeasonId",
                        column: x => x.SeasonId,
                        principalTable: "LeaderboardSeasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LeaderboardSeasonArchiveEntries_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LeaderboardSeasonProblems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProblemId = table.Column<Guid>(type: "uuid", nullable: false),
                    BaseScore = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaderboardSeasonProblems", x => x.Id);
                    table.CheckConstraint("CK_LeaderboardSeasonProblems_BaseScore", "\"BaseScore\" >= 0");
                    table.ForeignKey(
                        name: "FK_LeaderboardSeasonProblems_LeaderboardSeasons_SeasonId",
                        column: x => x.SeasonId,
                        principalTable: "LeaderboardSeasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LeaderboardSeasonProblems_Problems_ProblemId",
                        column: x => x.ProblemId,
                        principalTable: "Problems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LeaderboardSeasonArchiveProblemScores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArchiveEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProblemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProblemTitleSnapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BaseScore = table.Column<int>(type: "integer", nullable: false),
                    EarnedBaseScore = table.Column<int>(type: "integer", nullable: false),
                    TimeBonus = table.Column<int>(type: "integer", nullable: false),
                    RuntimeBonus = table.Column<int>(type: "integer", nullable: false),
                    MemoryBonus = table.Column<int>(type: "integer", nullable: false),
                    FinalProblemScore = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaderboardSeasonArchiveProblemScores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeaderboardSeasonArchiveProblemScores_LeaderboardSeasonArch~",
                        column: x => x.ArchiveEntryId,
                        principalTable: "LeaderboardSeasonArchiveEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LeaderboardSeasonArchiveProblemScores_LeaderboardSeasons_Se~",
                        column: x => x.SeasonId,
                        principalTable: "LeaderboardSeasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LeaderboardUserProblemScores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonProblemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProblemId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    BestBaseScore = table.Column<int>(type: "integer", nullable: false),
                    IsFullScore = table.Column<bool>(type: "boolean", nullable: false),
                    FirstFullScoreAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    BestPerformanceSubmissionId = table.Column<Guid>(type: "uuid", nullable: true),
                    BestRuntimeMs = table.Column<int>(type: "integer", nullable: true),
                    BestMemoryKb = table.Column<int>(type: "integer", nullable: true),
                    LastScoreImprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaderboardUserProblemScores", x => x.Id);
                    table.CheckConstraint("CK_LeaderboardUserProblemScores_BaseScore", "\"BestBaseScore\" >= 0");
                    table.ForeignKey(
                        name: "FK_LeaderboardUserProblemScores_LeaderboardSeasonProblems_Seas~",
                        column: x => x.SeasonProblemId,
                        principalTable: "LeaderboardSeasonProblems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LeaderboardUserProblemScores_LeaderboardSeasons_SeasonId",
                        column: x => x.SeasonId,
                        principalTable: "LeaderboardSeasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LeaderboardUserProblemScores_Problems_ProblemId",
                        column: x => x.ProblemId,
                        principalTable: "Problems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeaderboardUserProblemScores_Submissions_BestPerformanceSub~",
                        column: x => x.BestPerformanceSubmissionId,
                        principalTable: "Submissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_LeaderboardUserProblemScores_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LeaderboardSeasonAliases_SeasonId_Alias",
                table: "LeaderboardSeasonAliases",
                columns: new[] { "SeasonId", "Alias" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeaderboardSeasonAliases_SeasonId_UserId",
                table: "LeaderboardSeasonAliases",
                columns: new[] { "SeasonId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeaderboardSeasonAliases_UserId",
                table: "LeaderboardSeasonAliases",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaderboardSeasonArchiveEntries_SeasonId_FinalRank",
                table: "LeaderboardSeasonArchiveEntries",
                columns: new[] { "SeasonId", "FinalRank" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeaderboardSeasonArchiveEntries_SeasonId_UserId",
                table: "LeaderboardSeasonArchiveEntries",
                columns: new[] { "SeasonId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeaderboardSeasonArchiveEntries_UserId",
                table: "LeaderboardSeasonArchiveEntries",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaderboardSeasonArchiveProblemScores_ArchiveEntryId_Proble~",
                table: "LeaderboardSeasonArchiveProblemScores",
                columns: new[] { "ArchiveEntryId", "ProblemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeaderboardSeasonArchiveProblemScores_SeasonId",
                table: "LeaderboardSeasonArchiveProblemScores",
                column: "SeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaderboardSeasonProblems_ProblemId",
                table: "LeaderboardSeasonProblems",
                column: "ProblemId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaderboardSeasonProblems_SeasonId_ProblemId",
                table: "LeaderboardSeasonProblems",
                columns: new[] { "SeasonId", "ProblemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeaderboardSeasons_CreatedByUserId",
                table: "LeaderboardSeasons",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaderboardSeasons_IsCurrent",
                table: "LeaderboardSeasons",
                column: "IsCurrent",
                unique: true,
                filter: "\"IsCurrent\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_LeaderboardUserProblemScores_BestPerformanceSubmissionId",
                table: "LeaderboardUserProblemScores",
                column: "BestPerformanceSubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaderboardUserProblemScores_ProblemId",
                table: "LeaderboardUserProblemScores",
                column: "ProblemId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaderboardUserProblemScores_SeasonId_ProblemId_UserId",
                table: "LeaderboardUserProblemScores",
                columns: new[] { "SeasonId", "ProblemId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeaderboardUserProblemScores_SeasonProblemId_UserId",
                table: "LeaderboardUserProblemScores",
                columns: new[] { "SeasonProblemId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeaderboardUserProblemScores_UserId",
                table: "LeaderboardUserProblemScores",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LeaderboardSeasonAliases");

            migrationBuilder.DropTable(
                name: "LeaderboardSeasonArchiveProblemScores");

            migrationBuilder.DropTable(
                name: "LeaderboardUserProblemScores");

            migrationBuilder.DropTable(
                name: "LeaderboardSeasonArchiveEntries");

            migrationBuilder.DropTable(
                name: "LeaderboardSeasonProblems");

            migrationBuilder.DropTable(
                name: "LeaderboardSeasons");

            migrationBuilder.DropColumn(
                name: "IsLeaderboardAnonymous",
                table: "Users");
        }
    }
}
