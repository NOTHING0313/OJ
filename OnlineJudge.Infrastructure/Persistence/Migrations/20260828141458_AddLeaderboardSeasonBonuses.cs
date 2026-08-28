using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnlineJudge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLeaderboardSeasonBonuses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "BestPerformanceFinishedAt",
                table: "LeaderboardUserProblemScores",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BestPerformanceLanguage",
                table: "LeaderboardUserProblemScores",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FirstFullSubmissionId",
                table: "LeaderboardUserProblemScores",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ScoringRulesJson",
                table: "LeaderboardSeasons",
                type: "jsonb",
                nullable: false,
                defaultValue: "{\"timeBonusPercentages\":[20,16,13,10,8,6,5,4,3,2],\"runtimeBonusTiers\":[{\"maxRatioPercentage\":50,\"bonusPercentage\":6},{\"maxRatioPercentage\":65,\"bonusPercentage\":5},{\"maxRatioPercentage\":80,\"bonusPercentage\":3},{\"maxRatioPercentage\":100,\"bonusPercentage\":1}],\"memoryBonusTiers\":[{\"maxRatioPercentage\":50,\"bonusPercentage\":4},{\"maxRatioPercentage\":70,\"bonusPercentage\":3},{\"maxRatioPercentage\":85,\"bonusPercentage\":2},{\"maxRatioPercentage\":100,\"bonusPercentage\":1}]}");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FirstFullScoreAt",
                table: "LeaderboardSeasonArchiveProblemScores",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<int>(
                name: "MemoryBaselineKb",
                table: "LeaderboardSeasonArchiveProblemScores",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MemoryKb",
                table: "LeaderboardSeasonArchiveProblemScores",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PerformanceLanguage",
                table: "LeaderboardSeasonArchiveProblemScores",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RuntimeBaselineMs",
                table: "LeaderboardSeasonArchiveProblemScores",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RuntimeMs",
                table: "LeaderboardSeasonArchiveProblemScores",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TimeRank",
                table: "LeaderboardSeasonArchiveProblemScores",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FinalMemoryBonus",
                table: "LeaderboardSeasonArchiveEntries",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FinalRuntimeBonus",
                table: "LeaderboardSeasonArchiveEntries",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FinalTimeBonus",
                table: "LeaderboardSeasonArchiveEntries",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "LeaderboardSeasonProblemBenchmarks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonProblemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Language = table.Column<int>(type: "integer", nullable: false),
                    RuntimeBaselineMs = table.Column<int>(type: "integer", nullable: false),
                    MemoryBaselineKb = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaderboardSeasonProblemBenchmarks", x => x.Id);
                    table.CheckConstraint("CK_LeaderboardSeasonProblemBenchmarks_Memory", "\"MemoryBaselineKb\" > 0");
                    table.CheckConstraint("CK_LeaderboardSeasonProblemBenchmarks_Runtime", "\"RuntimeBaselineMs\" > 0");
                    table.ForeignKey(
                        name: "FK_LeaderboardSeasonProblemBenchmarks_LeaderboardSeasonProblem~",
                        column: x => x.SeasonProblemId,
                        principalTable: "LeaderboardSeasonProblems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LeaderboardUserProblemScores_FirstFullSubmissionId",
                table: "LeaderboardUserProblemScores",
                column: "FirstFullSubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaderboardSeasonProblemBenchmarks_SeasonProblemId_Language",
                table: "LeaderboardSeasonProblemBenchmarks",
                columns: new[] { "SeasonProblemId", "Language" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_LeaderboardUserProblemScores_Submissions_FirstFullSubmissio~",
                table: "LeaderboardUserProblemScores",
                column: "FirstFullSubmissionId",
                principalTable: "Submissions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LeaderboardUserProblemScores_Submissions_FirstFullSubmissio~",
                table: "LeaderboardUserProblemScores");

            migrationBuilder.DropTable(
                name: "LeaderboardSeasonProblemBenchmarks");

            migrationBuilder.DropIndex(
                name: "IX_LeaderboardUserProblemScores_FirstFullSubmissionId",
                table: "LeaderboardUserProblemScores");

            migrationBuilder.DropColumn(
                name: "BestPerformanceFinishedAt",
                table: "LeaderboardUserProblemScores");

            migrationBuilder.DropColumn(
                name: "BestPerformanceLanguage",
                table: "LeaderboardUserProblemScores");

            migrationBuilder.DropColumn(
                name: "FirstFullSubmissionId",
                table: "LeaderboardUserProblemScores");

            migrationBuilder.DropColumn(
                name: "ScoringRulesJson",
                table: "LeaderboardSeasons");

            migrationBuilder.DropColumn(
                name: "FirstFullScoreAt",
                table: "LeaderboardSeasonArchiveProblemScores");

            migrationBuilder.DropColumn(
                name: "MemoryBaselineKb",
                table: "LeaderboardSeasonArchiveProblemScores");

            migrationBuilder.DropColumn(
                name: "MemoryKb",
                table: "LeaderboardSeasonArchiveProblemScores");

            migrationBuilder.DropColumn(
                name: "PerformanceLanguage",
                table: "LeaderboardSeasonArchiveProblemScores");

            migrationBuilder.DropColumn(
                name: "RuntimeBaselineMs",
                table: "LeaderboardSeasonArchiveProblemScores");

            migrationBuilder.DropColumn(
                name: "RuntimeMs",
                table: "LeaderboardSeasonArchiveProblemScores");

            migrationBuilder.DropColumn(
                name: "TimeRank",
                table: "LeaderboardSeasonArchiveProblemScores");

            migrationBuilder.DropColumn(
                name: "FinalMemoryBonus",
                table: "LeaderboardSeasonArchiveEntries");

            migrationBuilder.DropColumn(
                name: "FinalRuntimeBonus",
                table: "LeaderboardSeasonArchiveEntries");

            migrationBuilder.DropColumn(
                name: "FinalTimeBonus",
                table: "LeaderboardSeasonArchiveEntries");
        }
    }
}
