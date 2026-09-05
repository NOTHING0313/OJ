using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineJudge.Domain.Entities;

namespace OnlineJudge.Infrastructure.Persistence.Configurations;

public class SubmissionChoiceSelectionConfiguration : IEntityTypeConfiguration<SubmissionChoiceSelection>
{
    public void Configure(EntityTypeBuilder<SubmissionChoiceSelection> builder)
    {
        builder.ToTable("SubmissionChoiceSelections");
        builder.HasKey(selection => new { selection.QuestionResultId, selection.RevisionOptionId });
        builder.HasOne(selection => selection.RevisionOption)
            .WithMany()
            .HasForeignKey(selection => selection.RevisionOptionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
