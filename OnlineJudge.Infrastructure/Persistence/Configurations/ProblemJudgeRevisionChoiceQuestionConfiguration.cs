using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineJudge.Domain.Entities;

namespace OnlineJudge.Infrastructure.Persistence.Configurations;

public class ProblemJudgeRevisionChoiceQuestionConfiguration : IEntityTypeConfiguration<ProblemJudgeRevisionChoiceQuestion>
{
    public void Configure(EntityTypeBuilder<ProblemJudgeRevisionChoiceQuestion> builder)
    {
        builder.ToTable("ProblemJudgeRevisionChoiceQuestions", table =>
        {
            table.HasCheckConstraint("CK_ProblemJudgeRevisionChoiceQuestions_Order", "\"Order\" >= 0");
            table.HasCheckConstraint("CK_ProblemJudgeRevisionChoiceQuestions_Mode", "\"SelectionMode\" IN (1, 2)");
            table.HasCheckConstraint("CK_ProblemJudgeRevisionChoiceQuestions_Score", "\"Score\" BETWEEN 1 AND 1000");
        });
        builder.HasKey(question => question.Id);
        builder.HasIndex(question => new { question.ProblemJudgeRevisionId, question.Order }).IsUnique();
        builder.HasIndex(question => new { question.ProblemJudgeRevisionId, question.SourceQuestionId }).IsUnique();
        builder.Property(question => question.StemMarkdown).HasColumnType("text").IsRequired();
        builder.Property(question => question.ExplanationMarkdown).HasColumnType("text").IsRequired();
        builder.Property(question => question.SelectionMode).HasConversion<int>().IsRequired();
        builder.HasOne(question => question.ProblemJudgeRevision)
            .WithMany(revision => revision.ChoiceQuestions)
            .HasForeignKey(question => question.ProblemJudgeRevisionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(question => question.Options)
            .WithOne(option => option.RevisionQuestion)
            .HasForeignKey(option => option.RevisionQuestionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
