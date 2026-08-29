using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnlineJudge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLeaderboardSeasonBoardsAndUx : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ScoringRulesJson",
                table: "LeaderboardSeasons",
                type: "jsonb",
                nullable: false,
                defaultValue: "{\"firstCompletionBonusEnabled\":true,\"runtimeBonusEnabled\":true,\"memoryBonusEnabled\":true,\"timeBonusPercentages\":[20,16,13,10,8,6,5,4,3,2],\"runtimeBonusTiers\":[{\"maxRatioPercentage\":50,\"bonusPercentage\":6},{\"maxRatioPercentage\":65,\"bonusPercentage\":5},{\"maxRatioPercentage\":80,\"bonusPercentage\":3},{\"maxRatioPercentage\":100,\"bonusPercentage\":1}],\"memoryBonusTiers\":[{\"maxRatioPercentage\":50,\"bonusPercentage\":4},{\"maxRatioPercentage\":70,\"bonusPercentage\":3},{\"maxRatioPercentage\":85,\"bonusPercentage\":2},{\"maxRatioPercentage\":100,\"bonusPercentage\":1}]}",
                oldClrType: typeof(string),
                oldType: "jsonb",
                oldDefaultValue: "{\"timeBonusPercentages\":[20,16,13,10,8,6,5,4,3,2],\"runtimeBonusTiers\":[{\"maxRatioPercentage\":50,\"bonusPercentage\":6},{\"maxRatioPercentage\":65,\"bonusPercentage\":5},{\"maxRatioPercentage\":80,\"bonusPercentage\":3},{\"maxRatioPercentage\":100,\"bonusPercentage\":1}],\"memoryBonusTiers\":[{\"maxRatioPercentage\":50,\"bonusPercentage\":4},{\"maxRatioPercentage\":70,\"bonusPercentage\":3},{\"maxRatioPercentage\":85,\"bonusPercentage\":2},{\"maxRatioPercentage\":100,\"bonusPercentage\":1}]}");

            migrationBuilder.CreateTable(
                name: "LeaderboardSeasonBoards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    BoardType = table.Column<int>(type: "integer", nullable: false),
                    ChallengeId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaderboardSeasonBoards", x => x.Id);
                    table.CheckConstraint("CK_LeaderboardSeasonBoards_Target", "(\"BoardType\" = 1 AND \"ChallengeId\" IS NULL) OR (\"BoardType\" = 2 AND \"ChallengeId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_LeaderboardSeasonBoards_Challenges_ChallengeId",
                        column: x => x.ChallengeId,
                        principalTable: "Challenges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeaderboardSeasonBoards_LeaderboardSeasons_SeasonId",
                        column: x => x.SeasonId,
                        principalTable: "LeaderboardSeasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LeaderboardSeasonBoards_ChallengeId",
                table: "LeaderboardSeasonBoards",
                column: "ChallengeId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaderboardSeasonBoards_SeasonId_BoardType",
                table: "LeaderboardSeasonBoards",
                columns: new[] { "SeasonId", "BoardType" },
                unique: true,
                filter: "\"BoardType\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_LeaderboardSeasonBoards_SeasonId_ChallengeId",
                table: "LeaderboardSeasonBoards",
                columns: new[] { "SeasonId", "ChallengeId" },
                unique: true,
                filter: "\"ChallengeId\" IS NOT NULL");

            migrationBuilder.Sql("""
                INSERT INTO "LeaderboardSeasonBoards" ("Id", "SeasonId", "BoardType", "ChallengeId", "CreatedAt")
                SELECT md5("Id"::text || ':global')::uuid, "Id", 1, NULL, NOW()
                FROM "LeaderboardSeasons"
                WHERE "IsCurrent" = TRUE;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LeaderboardSeasonBoards");

            migrationBuilder.AlterColumn<string>(
                name: "ScoringRulesJson",
                table: "LeaderboardSeasons",
                type: "jsonb",
                nullable: false,
                defaultValue: "{\"timeBonusPercentages\":[20,16,13,10,8,6,5,4,3,2],\"runtimeBonusTiers\":[{\"maxRatioPercentage\":50,\"bonusPercentage\":6},{\"maxRatioPercentage\":65,\"bonusPercentage\":5},{\"maxRatioPercentage\":80,\"bonusPercentage\":3},{\"maxRatioPercentage\":100,\"bonusPercentage\":1}],\"memoryBonusTiers\":[{\"maxRatioPercentage\":50,\"bonusPercentage\":4},{\"maxRatioPercentage\":70,\"bonusPercentage\":3},{\"maxRatioPercentage\":85,\"bonusPercentage\":2},{\"maxRatioPercentage\":100,\"bonusPercentage\":1}]}",
                oldClrType: typeof(string),
                oldType: "jsonb",
                oldDefaultValue: "{\"firstCompletionBonusEnabled\":true,\"runtimeBonusEnabled\":true,\"memoryBonusEnabled\":true,\"timeBonusPercentages\":[20,16,13,10,8,6,5,4,3,2],\"runtimeBonusTiers\":[{\"maxRatioPercentage\":50,\"bonusPercentage\":6},{\"maxRatioPercentage\":65,\"bonusPercentage\":5},{\"maxRatioPercentage\":80,\"bonusPercentage\":3},{\"maxRatioPercentage\":100,\"bonusPercentage\":1}],\"memoryBonusTiers\":[{\"maxRatioPercentage\":50,\"bonusPercentage\":4},{\"maxRatioPercentage\":70,\"bonusPercentage\":3},{\"maxRatioPercentage\":85,\"bonusPercentage\":2},{\"maxRatioPercentage\":100,\"bonusPercentage\":1}]}");
        }
    }
}
