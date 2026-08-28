using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineJudge.Domain.Entities;

namespace OnlineJudge.Infrastructure.Persistence.Configurations;

public class LeaderboardSeasonArchiveEntryConfiguration : IEntityTypeConfiguration<LeaderboardSeasonArchiveEntry>
{
    public void Configure(EntityTypeBuilder<LeaderboardSeasonArchiveEntry> builder)
    {
        builder.ToTable("LeaderboardSeasonArchiveEntries");
        builder.HasKey(entry => entry.Id);
        builder.HasIndex(entry => new { entry.SeasonId, entry.UserId }).IsUnique();
        builder.HasIndex(entry => new { entry.SeasonId, entry.FinalRank }).IsUnique();
        builder.Property(entry => entry.Alias).HasMaxLength(11).IsRequired();
        builder.Property(entry => entry.DisplayNameSnapshot).HasMaxLength(64).IsRequired();
        builder.Property(entry => entry.FinalRank).IsRequired();
        builder.Property(entry => entry.FinalScore).IsRequired();
        builder.Property(entry => entry.FinalBaseScore).IsRequired();
        builder.Property(entry => entry.FinalTimeBonus).IsRequired();
        builder.Property(entry => entry.FinalRuntimeBonus).IsRequired();
        builder.Property(entry => entry.FinalMemoryBonus).IsRequired();
        builder.Property(entry => entry.SolvedCount).IsRequired();
        builder.Property(entry => entry.LastScoreImprovedAt).IsRequired();
        builder.Property(entry => entry.CreatedAt).IsRequired();

        builder.HasOne(entry => entry.Season)
            .WithMany(season => season.ArchiveEntries)
            .HasForeignKey(entry => entry.SeasonId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(entry => entry.User)
            .WithMany()
            .HasForeignKey(entry => entry.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
