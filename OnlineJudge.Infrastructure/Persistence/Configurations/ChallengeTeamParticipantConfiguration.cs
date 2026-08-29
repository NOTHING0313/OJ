using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineJudge.Domain.Entities;

namespace OnlineJudge.Infrastructure.Persistence.Configurations;

public class ChallengeTeamParticipantConfiguration : IEntityTypeConfiguration<ChallengeTeamParticipant>
{
    public void Configure(EntityTypeBuilder<ChallengeTeamParticipant> builder)
    {
        builder.ToTable("ChallengeTeamParticipants");
        builder.HasKey(participant => participant.Id);
        builder.HasIndex(participant => new { participant.ChallengeId, participant.TeamId }).IsUnique();
        builder.Property(participant => participant.TeamNameSnapshot).HasMaxLength(40).IsRequired();
        builder.Property(participant => participant.ProjectNameSnapshot).HasMaxLength(80);
        builder.Property(participant => participant.RepositoryUrlSnapshot).HasMaxLength(2048);
        builder.Property(participant => participant.RegisteredAt).IsRequired();
        builder.HasOne(participant => participant.Challenge)
            .WithMany(challenge => challenge.TeamParticipants)
            .HasForeignKey(participant => participant.ChallengeId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(participant => participant.Team)
            .WithMany(team => team.ChallengeParticipations)
            .HasForeignKey(participant => participant.TeamId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(participant => participant.RegisteredByUser)
            .WithMany()
            .HasForeignKey(participant => participant.RegisteredByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(participant => participant.SelectedTeamProject)
            .WithMany(project => project.ChallengeParticipations)
            .HasForeignKey(participant => participant.SelectedTeamProjectId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
