using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineJudge.Domain.Entities;

namespace OnlineJudge.Infrastructure.Persistence.Configurations;

public class ChallengeTaskCompletionConfiguration : IEntityTypeConfiguration<ChallengeTaskCompletion>
{
    public void Configure(EntityTypeBuilder<ChallengeTaskCompletion> builder)
    {
        builder.ToTable("ChallengeTaskCompletions");

        builder.HasKey(completion => completion.Id);

        builder.HasIndex(completion => new { completion.UserId, completion.ChallengeTaskId })
            .IsUnique();

        builder.Property(completion => completion.CompletedAt)
            .IsRequired();

        builder.Property(completion => completion.Score)
            .IsRequired();

        builder.HasOne(completion => completion.Challenge)
            .WithMany(challenge => challenge.Completions)
            .HasForeignKey(completion => completion.ChallengeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(completion => completion.ChallengeTask)
            .WithMany(task => task.Completions)
            .HasForeignKey(completion => completion.ChallengeTaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(completion => completion.User)
            .WithMany()
            .HasForeignKey(completion => completion.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(completion => completion.Submission)
            .WithMany()
            .HasForeignKey(completion => completion.SubmissionId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
