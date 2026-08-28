using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineJudge.Domain.Entities;

namespace OnlineJudge.Infrastructure.Persistence.Configurations;

public class TeamInvitationConfiguration : IEntityTypeConfiguration<TeamInvitation>
{
    public void Configure(EntityTypeBuilder<TeamInvitation> builder)
    {
        builder.ToTable("TeamInvitations");
        builder.HasKey(invitation => invitation.Id);
        builder.HasIndex(invitation => new { invitation.TeamId, invitation.InvitedUserId })
            .IsUnique()
            .HasFilter("\"Status\" = 1");
        builder.Property(invitation => invitation.Status).HasConversion<int>().IsRequired();
        builder.Property(invitation => invitation.CreatedAt).IsRequired();
        builder.HasOne(invitation => invitation.Team)
            .WithMany(team => team.Invitations)
            .HasForeignKey(invitation => invitation.TeamId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(invitation => invitation.InvitedUser)
            .WithMany(user => user.TeamInvitations)
            .HasForeignKey(invitation => invitation.InvitedUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(invitation => invitation.InvitedByUser)
            .WithMany(user => user.SentTeamInvitations)
            .HasForeignKey(invitation => invitation.InvitedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
