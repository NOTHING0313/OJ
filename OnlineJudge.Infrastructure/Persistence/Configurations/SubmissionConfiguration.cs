using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineJudge.Domain.Entities;

namespace OnlineJudge.Infrastructure.Persistence.Configurations;

public class SubmissionConfiguration : IEntityTypeConfiguration<Submission>
{
    public void Configure(EntityTypeBuilder<Submission> builder)
    {
        builder.ToTable("Submissions", table => table.HasCheckConstraint(
            "CK_Submissions_KindPayload",
            "(\"SubmissionKind\" = 1 AND \"Language\" IS NOT NULL AND \"SourceCode\" IS NOT NULL) OR (\"SubmissionKind\" = 2 AND \"Language\" IS NULL AND \"SourceCode\" IS NULL)"));

        builder.HasKey(submission => submission.Id);

        builder.Property(submission => submission.ProblemId)
            .IsRequired();

        builder.Property(submission => submission.ProblemJudgeRevisionId);

        builder.Property(submission => submission.UserId)
            .IsRequired();

        builder.Property(submission => submission.ChallengeTaskId);

        builder.Property(submission => submission.ChallengeTeamParticipantId);

        builder.Property(submission => submission.SubmissionKind)
            .HasConversion<int>()
            .IsRequired()
            .HasDefaultValue(Domain.Enums.SubmissionKind.Code);

        builder.Property(submission => submission.Language).HasConversion<int?>();

        builder.Property(submission => submission.SourceCode).HasColumnType("text");

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

        builder.HasOne(submission => submission.ProblemJudgeRevision)
            .WithMany()
            .HasForeignKey(submission => submission.ProblemJudgeRevisionId)
            .OnDelete(DeleteBehavior.Restrict);

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
