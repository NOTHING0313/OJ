using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineJudge.Domain.Entities;

namespace OnlineJudge.Infrastructure.Persistence.Configurations;

public class LeaderboardSeasonArchiveProblemScoreConfiguration : IEntityTypeConfiguration<LeaderboardSeasonArchiveProblemScore>
{
    public void Configure(EntityTypeBuilder<LeaderboardSeasonArchiveProblemScore> builder)
    {
        builder.ToTable("LeaderboardSeasonArchiveProblemScores");
        builder.HasKey(score => score.Id);
        builder.HasIndex(score => new { score.ArchiveEntryId, score.ProblemId }).IsUnique();
        builder.Property(score => score.ProblemTitleSnapshot).HasMaxLength(200).IsRequired();
        builder.Property(score => score.BaseScore).IsRequired();
        builder.Property(score => score.EarnedBaseScore).IsRequired();
        builder.Property(score => score.TimeBonus).IsRequired();
        builder.Property(score => score.FirstFullScoreAt).IsRequired();
        builder.Property(score => score.PerformanceLanguage).HasConversion<int?>();
        builder.Property(score => score.RuntimeBonus).IsRequired();
        builder.Property(score => score.MemoryBonus).IsRequired();
        builder.Property(score => score.FinalProblemScore).IsRequired();

        builder.HasOne(score => score.Season)
            .WithMany()
            .HasForeignKey(score => score.SeasonId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(score => score.ArchiveEntry)
            .WithMany(entry => entry.ProblemScores)
            .HasForeignKey(score => score.ArchiveEntryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
