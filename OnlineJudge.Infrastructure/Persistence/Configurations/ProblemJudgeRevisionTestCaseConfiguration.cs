using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineJudge.Domain.Entities;

namespace OnlineJudge.Infrastructure.Persistence.Configurations;

public class ProblemJudgeRevisionTestCaseConfiguration : IEntityTypeConfiguration<ProblemJudgeRevisionTestCase>
{
    public void Configure(EntityTypeBuilder<ProblemJudgeRevisionTestCase> builder)
    {
        builder.ToTable("ProblemJudgeRevisionTestCases");
        builder.HasKey(testCase => testCase.Id);
        builder.HasIndex(testCase => new { testCase.ProblemJudgeRevisionId, testCase.Order }).IsUnique();
        builder.HasIndex(testCase => new { testCase.ProblemJudgeRevisionId, testCase.SourceTestCaseId }).IsUnique();

        builder.Property(testCase => testCase.Input).HasColumnType("text").IsRequired();
        builder.Property(testCase => testCase.ExpectedOutput).HasColumnType("text").IsRequired();
        builder.Property(testCase => testCase.ArgumentsJson).HasColumnType("text");
        builder.Property(testCase => testCase.ExpectedJson).HasColumnType("text");
        builder.Property(testCase => testCase.Visibility).HasConversion<int>().IsRequired();
        builder.Property(testCase => testCase.Score).IsRequired();

        builder.HasOne(testCase => testCase.ProblemJudgeRevision)
            .WithMany(revision => revision.TestCases)
            .HasForeignKey(testCase => testCase.ProblemJudgeRevisionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(testCase => testCase.SourceTestCase)
            .WithMany(source => source.JudgeRevisionTestCases)
            .HasForeignKey(testCase => testCase.SourceTestCaseId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
