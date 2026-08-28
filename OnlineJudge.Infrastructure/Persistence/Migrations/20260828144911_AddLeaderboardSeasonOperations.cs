using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnlineJudge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLeaderboardSeasonOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ActivatedAt",
                table: "LeaderboardSeasons",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ArchivedAt",
                table: "LeaderboardSeasons",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FinalizedAt",
                table: "LeaderboardSeasons",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FrozenAt",
                table: "LeaderboardSeasons",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ManuallyFrozenAt",
                table: "LeaderboardSeasons",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LeaderboardSeasonRankSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Rank = table.Column<int>(type: "integer", nullable: false),
                    TotalScore = table.Column<int>(type: "integer", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaderboardSeasonRankSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeaderboardSeasonRankSnapshots_LeaderboardSeasons_SeasonId",
                        column: x => x.SeasonId,
                        principalTable: "LeaderboardSeasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LeaderboardSeasonRankSnapshots_SeasonId_RecordedAt",
                table: "LeaderboardSeasonRankSnapshots",
                columns: new[] { "SeasonId", "RecordedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LeaderboardSeasonRankSnapshots_SeasonId_UserId_RecordedAt",
                table: "LeaderboardSeasonRankSnapshots",
                columns: new[] { "SeasonId", "UserId", "RecordedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LeaderboardSeasonRankSnapshots");

            migrationBuilder.DropColumn(
                name: "ActivatedAt",
                table: "LeaderboardSeasons");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "LeaderboardSeasons");

            migrationBuilder.DropColumn(
                name: "FinalizedAt",
                table: "LeaderboardSeasons");

            migrationBuilder.DropColumn(
                name: "FrozenAt",
                table: "LeaderboardSeasons");

            migrationBuilder.DropColumn(
                name: "ManuallyFrozenAt",
                table: "LeaderboardSeasons");
        }
    }
}
