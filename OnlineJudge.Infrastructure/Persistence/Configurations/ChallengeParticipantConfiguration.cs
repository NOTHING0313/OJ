using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineJudge.Domain.Entities;

namespace OnlineJudge.Infrastructure.Persistence.Configurations;

public class ChallengeParticipantConfiguration : IEntityTypeConfiguration<ChallengeParticipant>
{
    public void Configure(EntityTypeBuilder<ChallengeParticipant> builder)
    {
        builder.ToTable("ChallengeParticipants");

        builder.HasKey(participant => participant.Id);

        builder.HasIndex(participant => new { participant.ChallengeId, participant.UserId })
            .IsUnique();

        builder.Property(participant => participant.JoinedAt)
            .IsRequired();

        builder.HasOne(participant => participant.Challenge)
            .WithMany(challenge => challenge.Participants)
            .HasForeignKey(participant => participant.ChallengeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(participant => participant.User)
            .WithMany(user => user.ChallengeParticipants)
            .HasForeignKey(participant => participant.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
