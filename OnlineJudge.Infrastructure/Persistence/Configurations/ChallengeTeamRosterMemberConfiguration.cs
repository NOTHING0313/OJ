using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineJudge.Domain.Entities;

namespace OnlineJudge.Infrastructure.Persistence.Configurations;

public class ChallengeTeamRosterMemberConfiguration : IEntityTypeConfiguration<ChallengeTeamRosterMember>
{
    public void Configure(EntityTypeBuilder<ChallengeTeamRosterMember> builder)
    {
        builder.ToTable("ChallengeTeamRosterMembers");
        builder.HasKey(member => member.Id);
        builder.HasIndex(member => new { member.ChallengeTeamParticipantId, member.UserId }).IsUnique();
        builder.HasIndex(member => new { member.ChallengeId, member.UserId }).IsUnique();
        builder.Property(member => member.UserNameSnapshot).HasMaxLength(100).IsRequired();
        builder.Property(member => member.TeamMemberRoleSnapshot).HasConversion<int>().IsRequired();
        builder.Property(member => member.RegisteredAt).IsRequired();
        builder.HasOne(member => member.ChallengeTeamParticipant)
            .WithMany(participant => participant.RosterMembers)
            .HasForeignKey(member => member.ChallengeTeamParticipantId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(member => member.Challenge)
            .WithMany(challenge => challenge.TeamRosterMembers)
            .HasForeignKey(member => member.ChallengeId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(member => member.Team)
            .WithMany()
            .HasForeignKey(member => member.TeamId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(member => member.User)
            .WithMany()
            .HasForeignKey(member => member.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
