using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineJudge.Domain.Entities;
using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Infrastructure.Persistence.Configurations;

public class LeaderboardSeasonBoardConfiguration : IEntityTypeConfiguration<LeaderboardSeasonBoard>
{
    public void Configure(EntityTypeBuilder<LeaderboardSeasonBoard> builder)
    {
        builder.ToTable("LeaderboardSeasonBoards", table => table.HasCheckConstraint(
            "CK_LeaderboardSeasonBoards_Target",
            "(\"BoardType\" = 1 AND \"ChallengeId\" IS NULL) OR (\"BoardType\" = 2 AND \"ChallengeId\" IS NOT NULL)"));
        builder.HasKey(board => board.Id);
        builder.Property(board => board.BoardType).HasConversion<int>().IsRequired();
        builder.Property(board => board.CreatedAt).IsRequired();
        builder.HasIndex(board => new { board.SeasonId, board.BoardType })
            .IsUnique()
            .HasFilter("\"BoardType\" = 1");
        builder.HasIndex(board => new { board.SeasonId, board.ChallengeId })
            .IsUnique()
            .HasFilter("\"ChallengeId\" IS NOT NULL");
        builder.HasOne(board => board.Season)
            .WithMany(season => season.Boards)
            .HasForeignKey(board => board.SeasonId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(board => board.Challenge)
            .WithMany(challenge => challenge.LeaderboardSeasonBoards)
            .HasForeignKey(board => board.ChallengeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
