using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineJudge.Domain.Entities;

namespace OnlineJudge.Infrastructure.Persistence.Configurations;

public class TeamChatMessageConfiguration : IEntityTypeConfiguration<TeamChatMessage>
{
    public void Configure(EntityTypeBuilder<TeamChatMessage> builder)
    {
        builder.ToTable("TeamChatMessages");
        builder.HasKey(message => message.Id);
        builder.HasIndex(message => new { message.TeamId, message.CreatedAt, message.Id });
        builder.HasIndex(message => message.EventKey).IsUnique().HasFilter("\"EventKey\" IS NOT NULL");
        builder.Property(message => message.Type).IsRequired();
        builder.Property(message => message.Content).HasMaxLength(2000);
        builder.Property(message => message.EventKey).HasMaxLength(300);
        builder.Property(message => message.CreatedAt).IsRequired();
        builder.HasOne(message => message.Team)
            .WithMany(team => team.ChatMessages)
            .HasForeignKey(message => message.TeamId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(message => message.SenderUser)
            .WithMany()
            .HasForeignKey(message => message.SenderUserId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(message => message.RelatedChallenge)
            .WithMany()
            .HasForeignKey(message => message.RelatedChallengeId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(message => message.RelatedPeerReviewAssignment)
            .WithMany()
            .HasForeignKey(message => message.RelatedPeerReviewAssignmentId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
