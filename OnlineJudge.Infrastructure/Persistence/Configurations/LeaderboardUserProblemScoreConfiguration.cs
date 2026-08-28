using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineJudge.Domain.Entities;

namespace OnlineJudge.Infrastructure.Persistence.Configurations;

public class LeaderboardUserProblemScoreConfiguration : IEntityTypeConfiguration<LeaderboardUserProblemScore>
{
    public void Configure(EntityTypeBuilder<LeaderboardUserProblemScore> builder)
    {
        builder.ToTable("LeaderboardUserProblemScores", table =>
            table.HasCheckConstraint("CK_LeaderboardUserProblemScores_BaseScore", "\"BestBaseScore\" >= 0"));

        builder.HasKey(score => score.Id);
        builder.HasIndex(score => new { score.SeasonId, score.ProblemId, score.UserId }).IsUnique();
        builder.HasIndex(score => new { score.SeasonProblemId, score.UserId }).IsUnique();
        builder.Property(score => score.BestBaseScore).IsRequired();
        builder.Property(score => score.IsFullScore).IsRequired();
        builder.Property(score => score.LastScoreImprovedAt).IsRequired();
        builder.Property(score => score.CreatedAt).IsRequired();
        builder.Property(score => score.UpdatedAt).IsRequired();

        builder.HasOne(score => score.Season)
            .WithMany(season => season.UserProblemScores)
            .HasForeignKey(score => score.SeasonId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(score => score.SeasonProblem)
            .WithMany(problem => problem.UserScores)
            .HasForeignKey(score => score.SeasonProblemId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(score => score.Problem)
            .WithMany()
            .HasForeignKey(score => score.ProblemId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(score => score.User)
            .WithMany()
            .HasForeignKey(score => score.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(score => score.BestPerformanceSubmission)
            .WithMany()
            .HasForeignKey(score => score.BestPerformanceSubmissionId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
