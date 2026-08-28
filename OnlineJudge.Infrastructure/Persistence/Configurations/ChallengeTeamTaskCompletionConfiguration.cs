using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineJudge.Domain.Entities;

namespace OnlineJudge.Infrastructure.Persistence.Configurations;

public class ChallengeTeamTaskCompletionConfiguration : IEntityTypeConfiguration<ChallengeTeamTaskCompletion>
{
    public void Configure(EntityTypeBuilder<ChallengeTeamTaskCompletion> builder)
    {
        builder.ToTable("ChallengeTeamTaskCompletions");
        builder.HasKey(completion => completion.Id);
        builder.HasIndex(completion => new { completion.ChallengeTeamParticipantId, completion.ChallengeTaskId }).IsUnique();
        builder.Property(completion => completion.Score).IsRequired();
        builder.Property(completion => completion.IsCompleted).IsRequired();
        builder.Property(completion => completion.CompletedAt).IsRequired();
        builder.Property(completion => completion.UpdatedAt).IsRequired();
        builder.HasOne(completion => completion.Challenge)
            .WithMany(challenge => challenge.TeamTaskCompletions)
            .HasForeignKey(completion => completion.ChallengeId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(completion => completion.ChallengeTask)
            .WithMany(task => task.TeamCompletions)
            .HasForeignKey(completion => completion.ChallengeTaskId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(completion => completion.ChallengeTeamParticipant)
            .WithMany(participant => participant.TaskCompletions)
            .HasForeignKey(completion => completion.ChallengeTeamParticipantId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(completion => completion.BestSubmission)
            .WithMany()
            .HasForeignKey(completion => completion.BestSubmissionId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(completion => completion.ContributorUser)
            .WithMany()
            .HasForeignKey(completion => completion.ContributorUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
