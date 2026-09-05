using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineJudge.Domain.Entities;

namespace OnlineJudge.Infrastructure.Persistence.Configurations;

public class ProblemJudgeRevisionChoiceOptionConfiguration : IEntityTypeConfiguration<ProblemJudgeRevisionChoiceOption>
{
    public void Configure(EntityTypeBuilder<ProblemJudgeRevisionChoiceOption> builder)
    {
        builder.ToTable("ProblemJudgeRevisionChoiceOptions", table => table.HasCheckConstraint("CK_ProblemJudgeRevisionChoiceOptions_Order", "\"Order\" >= 0"));
        builder.HasKey(option => option.Id);
        builder.HasIndex(option => new { option.RevisionQuestionId, option.Order }).IsUnique();
        builder.HasIndex(option => new { option.RevisionQuestionId, option.SourceOptionId }).IsUnique();
        builder.Property(option => option.ContentMarkdown).HasColumnType("text").IsRequired();
    }
}
