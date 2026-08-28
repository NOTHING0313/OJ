using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineJudge.Domain.Entities;

namespace OnlineJudge.Infrastructure.Persistence.Configurations;

public class SubmissionConfiguration : IEntityTypeConfiguration<Submission>
{
    public void Configure(EntityTypeBuilder<Submission> builder)
    {
        builder.ToTable("Submissions");

        builder.HasKey(submission => submission.Id);

        builder.Property(submission => submission.ProblemId)
            .IsRequired();

        builder.Property(submission => submission.UserId)
            .IsRequired();

        builder.Property(submission => submission.ChallengeTaskId);

        builder.Property(submission => submission.ChallengeTeamParticipantId);

        builder.Property(submission => submission.Language)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(submission => submission.SourceCode)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(submission => submission.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(submission => submission.TimeUsedMs);

        builder.Property(submission => submission.MemoryUsedKb);

        builder.Property(submission => submission.ErrorMessage)
            .HasColumnType("text")
            .IsRequired(false);

        builder.Property(submission => submission.CreatedAt)
            .IsRequired();

        builder.Property(submission => submission.FinishedAt);

        builder.HasMany(submission => submission.CaseResults)
            .WithOne(caseResult => caseResult.Submission)
            .HasForeignKey(caseResult => caseResult.SubmissionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(submission => submission.ChallengeTask)
            .WithMany()
            .HasForeignKey(submission => submission.ChallengeTaskId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(submission => submission.ChallengeTeamParticipant)
            .WithMany(participant => participant.Submissions)
            .HasForeignKey(submission => submission.ChallengeTeamParticipantId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
