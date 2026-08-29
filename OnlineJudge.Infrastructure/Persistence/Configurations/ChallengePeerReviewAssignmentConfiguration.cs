using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineJudge.Domain.Entities;

namespace OnlineJudge.Infrastructure.Persistence.Configurations;

public class ChallengePeerReviewAssignmentConfiguration : IEntityTypeConfiguration<ChallengePeerReviewAssignment>
{
    public void Configure(EntityTypeBuilder<ChallengePeerReviewAssignment> builder)
    {
        builder.ToTable("ChallengePeerReviewAssignments");
        builder.HasKey(assignment => assignment.Id);
        builder.HasIndex(assignment => new { assignment.ChallengeId, assignment.ReviewerParticipantId }).IsUnique();
        builder.HasIndex(assignment => new { assignment.ChallengeId, assignment.TargetParticipantId }).IsUnique();
        builder.Property(assignment => assignment.ReviewerTeamNameSnapshot).HasMaxLength(40).IsRequired();
        builder.Property(assignment => assignment.TargetTeamNameSnapshot).HasMaxLength(40).IsRequired();
        builder.Property(assignment => assignment.TargetProjectNameSnapshot).HasMaxLength(80).IsRequired();
        builder.Property(assignment => assignment.TargetRepositoryUrlSnapshot).HasMaxLength(2048).IsRequired();
        builder.Property(assignment => assignment.CreatedAt).IsRequired();
        builder.HasOne(assignment => assignment.Challenge)
            .WithMany(challenge => challenge.PeerReviewAssignments)
            .HasForeignKey(assignment => assignment.ChallengeId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(assignment => assignment.ReviewerParticipant)
            .WithMany(participant => participant.ReviewAssignments)
            .HasForeignKey(assignment => assignment.ReviewerParticipantId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(assignment => assignment.TargetParticipant)
            .WithMany(participant => participant.TargetAssignments)
            .HasForeignKey(assignment => assignment.TargetParticipantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
