using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineJudge.Domain.Entities;

namespace OnlineJudge.Infrastructure.Persistence.Configurations;

public class LeaderboardSeasonRankSnapshotConfiguration : IEntityTypeConfiguration<LeaderboardSeasonRankSnapshot>
{
    public void Configure(EntityTypeBuilder<LeaderboardSeasonRankSnapshot> builder)
    {
        builder.ToTable("LeaderboardSeasonRankSnapshots");
        builder.HasKey(snapshot => snapshot.Id);
        builder.HasIndex(snapshot => new { snapshot.SeasonId, snapshot.RecordedAt });
        builder.HasIndex(snapshot => new { snapshot.SeasonId, snapshot.UserId, snapshot.RecordedAt });
        builder.Property(snapshot => snapshot.Rank).IsRequired();
        builder.Property(snapshot => snapshot.TotalScore).IsRequired();
        builder.Property(snapshot => snapshot.RecordedAt).IsRequired();
        builder.HasOne(snapshot => snapshot.Season)
            .WithMany(season => season.RankSnapshots)
            .HasForeignKey(snapshot => snapshot.SeasonId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
