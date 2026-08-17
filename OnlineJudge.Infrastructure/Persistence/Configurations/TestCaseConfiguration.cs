using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineJudge.Domain.Entities;

namespace OnlineJudge.Infrastructure.Persistence.Configurations;

public class TestCaseConfiguration : IEntityTypeConfiguration<TestCase>
{
    public void Configure(EntityTypeBuilder<TestCase> builder)
    {
        builder.ToTable("TestCases");

        builder.HasKey(testCase => testCase.Id);

        builder.Property(testCase => testCase.ProblemId)
            .IsRequired();

        builder.Property(testCase => testCase.Input)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(testCase => testCase.ExpectedOutput)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(testCase => testCase.ArgumentsJson)
            .HasColumnType("text");

        builder.Property(testCase => testCase.ExpectedJson)
            .HasColumnType("text");

        builder.Property(testCase => testCase.Visibility)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(testCase => testCase.Score)
            .IsRequired();

        builder.Property(testCase => testCase.CreatedAt)
            .IsRequired();
    }
}
