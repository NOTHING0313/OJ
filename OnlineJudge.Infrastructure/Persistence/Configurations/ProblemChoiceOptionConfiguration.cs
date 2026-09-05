using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineJudge.Domain.Entities;

namespace OnlineJudge.Infrastructure.Persistence.Configurations;

public class ProblemChoiceOptionConfiguration : IEntityTypeConfiguration<ProblemChoiceOption>
{
    public void Configure(EntityTypeBuilder<ProblemChoiceOption> builder)
    {
        builder.ToTable("ProblemChoiceOptions", table => table.HasCheckConstraint("CK_ProblemChoiceOptions_Order", "\"Order\" >= 0"));
        builder.HasKey(option => option.Id);
        builder.HasIndex(option => new { option.QuestionId, option.Order })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");
        builder.Property(option => option.ContentMarkdown).HasColumnType("text").IsRequired();
    }
}
