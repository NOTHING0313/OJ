using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineJudge.Domain.Entities;

namespace OnlineJudge.Infrastructure.Persistence.Configurations;

public class LeaderboardSeasonProblemConfiguration : IEntityTypeConfiguration<LeaderboardSeasonProblem>
{
    public void Configure(EntityTypeBuilder<LeaderboardSeasonProblem> builder)
    {
        builder.ToTable("LeaderboardSeasonProblems", table =>
            table.HasCheckConstraint("CK_LeaderboardSeasonProblems_BaseScore", "\"BaseScore\" >= 0"));

        builder.HasKey(item => item.Id);
        builder.HasIndex(item => new { item.SeasonId, item.ProblemId }).IsUnique();
        builder.Property(item => item.BaseScore).IsRequired();
        builder.Property(item => item.CreatedAt).IsRequired();

        builder.HasOne(item => item.Season)
            .WithMany(season => season.Problems)
            .HasForeignKey(item => item.SeasonId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(item => item.Problem)
            .WithMany()
            .HasForeignKey(item => item.ProblemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
