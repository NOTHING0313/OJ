using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineJudge.Domain.Entities;

namespace OnlineJudge.Infrastructure.Persistence.Configurations;

public class SubmissionCaseResultConfiguration : IEntityTypeConfiguration<SubmissionCaseResult>
{
    public void Configure(EntityTypeBuilder<SubmissionCaseResult> builder)
    {
        builder.ToTable("SubmissionCaseResults");

        builder.HasKey(caseResult => caseResult.Id);

        builder.Property(caseResult => caseResult.SubmissionId)
            .IsRequired();

        builder.Property(caseResult => caseResult.TestCaseId)
            .IsRequired();

        builder.Property(caseResult => caseResult.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(caseResult => caseResult.TimeUsedMs);

        builder.Property(caseResult => caseResult.MemoryUsedKb);

        builder.Property(caseResult => caseResult.ActualOutput)
            .HasColumnType("text")
            .IsRequired(false);

        builder.Property(caseResult => caseResult.ErrorMessage)
            .HasColumnType("text")
            .IsRequired(false);

        builder.HasOne(caseResult => caseResult.TestCase)
            .WithMany()
            .HasForeignKey(caseResult => caseResult.TestCaseId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
