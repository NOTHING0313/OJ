using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineJudge.Domain.Entities;
using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Infrastructure.Persistence.Configurations;

public class LeaderboardSeasonConfiguration : IEntityTypeConfiguration<LeaderboardSeason>
{
    public void Configure(EntityTypeBuilder<LeaderboardSeason> builder)
    {
        builder.ToTable("LeaderboardSeasons", table =>
            table.HasCheckConstraint("CK_LeaderboardSeasons_TimeOrder", "\"StartAt\" < \"FreezeAt\" AND \"FreezeAt\" < \"PublicUntil\""));

        builder.HasKey(season => season.Id);
        builder.HasIndex(season => season.IsCurrent)
            .IsUnique()
            .HasFilter("\"IsCurrent\" = TRUE");

        builder.Property(season => season.Name).HasMaxLength(120).IsRequired();
        builder.Property(season => season.Status).HasConversion<int>().HasDefaultValue(LeaderboardSeasonStatus.Scheduled).IsRequired();
        builder.Property(season => season.IsCurrent).HasDefaultValue(true).IsRequired();
        builder.Property(season => season.ScoringRulesJson)
            .HasColumnType("jsonb")
            .HasDefaultValue(OnlineJudge.Infrastructure.Leaderboards.LeaderboardScoringRulesSerializer.DefaultRulesJson)
            .IsRequired();
        builder.Property(season => season.StartAt).IsRequired();
        builder.Property(season => season.FreezeAt).IsRequired();
        builder.Property(season => season.PublicUntil).IsRequired();
        builder.Property(season => season.CreatedByUserId).IsRequired();
        builder.Property(season => season.CreatedAt).IsRequired();
        builder.Property(season => season.UpdatedAt).IsRequired();

        builder.HasOne(season => season.CreatedByUser)
            .WithMany()
            .HasForeignKey(season => season.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
