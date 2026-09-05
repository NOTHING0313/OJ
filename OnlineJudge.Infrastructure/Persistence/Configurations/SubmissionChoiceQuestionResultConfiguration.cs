using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineJudge.Domain.Entities;

namespace OnlineJudge.Infrastructure.Persistence.Configurations;

public class SubmissionChoiceQuestionResultConfiguration : IEntityTypeConfiguration<SubmissionChoiceQuestionResult>
{
    public void Configure(EntityTypeBuilder<SubmissionChoiceQuestionResult> builder)
    {
        builder.ToTable("SubmissionChoiceQuestionResults");
        builder.HasKey(result => result.Id);
        builder.HasIndex(result => new { result.SubmissionId, result.RevisionQuestionId }).IsUnique();
        builder.HasOne(result => result.Submission)
            .WithMany(submission => submission.ChoiceQuestionResults)
            .HasForeignKey(result => result.SubmissionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(result => result.RevisionQuestion)
            .WithMany()
            .HasForeignKey(result => result.RevisionQuestionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(result => result.Selections)
            .WithOne(selection => selection.QuestionResult)
            .HasForeignKey(selection => selection.QuestionResultId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
