using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineJudge.Domain.Entities;

namespace OnlineJudge.Infrastructure.Persistence.Configurations;

public class ProblemChoiceQuestionConfiguration : IEntityTypeConfiguration<ProblemChoiceQuestion>
{
    public void Configure(EntityTypeBuilder<ProblemChoiceQuestion> builder)
    {
        builder.ToTable("ProblemChoiceQuestions", table =>
        {
            table.HasCheckConstraint("CK_ProblemChoiceQuestions_Order", "\"Order\" >= 0");
            table.HasCheckConstraint("CK_ProblemChoiceQuestions_Mode", "\"SelectionMode\" IN (1, 2)");
            table.HasCheckConstraint("CK_ProblemChoiceQuestions_Score", "\"Score\" BETWEEN 1 AND 1000");
        });
        builder.HasKey(question => question.Id);
        builder.HasIndex(question => new { question.ProblemId, question.Order })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");
        builder.Property(question => question.StemMarkdown).HasColumnType("text").IsRequired();
        builder.Property(question => question.ExplanationMarkdown).HasColumnType("text").IsRequired();
        builder.Property(question => question.SelectionMode).HasConversion<int>().IsRequired();
        builder.HasMany(question => question.Options)
            .WithOne(option => option.Question)
            .HasForeignKey(option => option.QuestionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
