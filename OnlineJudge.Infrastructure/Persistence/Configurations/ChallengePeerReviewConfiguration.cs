using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineJudge.Domain.Entities;

namespace OnlineJudge.Infrastructure.Persistence.Configurations;

public class ChallengePeerReviewConfiguration : IEntityTypeConfiguration<ChallengePeerReview>
{
    public void Configure(EntityTypeBuilder<ChallengePeerReview> builder)
    {
        builder.ToTable("ChallengePeerReviews");
        builder.HasKey(review => review.Id);
        builder.HasIndex(review => review.AssignmentId).IsUnique();
        builder.Property(review => review.Status).HasConversion<int>().IsRequired();
        builder.Property(review => review.Summary).HasMaxLength(1000);
        builder.Property(review => review.Strengths).HasMaxLength(2000);
        builder.Property(review => review.Improvements).HasMaxLength(2000);
        builder.Property(review => review.UpdatedAt).IsRequired();
        builder.HasOne(review => review.Assignment)
            .WithOne(assignment => assignment.Review)
            .HasForeignKey<ChallengePeerReview>(review => review.AssignmentId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(review => review.Challenge)
            .WithMany(challenge => challenge.PeerReviews)
            .HasForeignKey(review => review.ChallengeId)
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(review => review.ReviewerParticipant)
            .WithMany()
            .HasForeignKey(review => review.ReviewerParticipantId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(review => review.TargetParticipant)
            .WithMany()
            .HasForeignKey(review => review.TargetParticipantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
