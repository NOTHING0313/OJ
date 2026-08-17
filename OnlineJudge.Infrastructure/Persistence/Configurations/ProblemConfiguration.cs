using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineJudge.Domain.Entities;
using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Infrastructure.Persistence.Configurations;

public class ProblemConfiguration : IEntityTypeConfiguration<Problem>
{
    public void Configure(EntityTypeBuilder<Problem> builder)
    {
        builder.ToTable("Problems");

        builder.HasKey(problem => problem.Id);

        builder.Property(problem => problem.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(problem => problem.Description)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(problem => problem.InputDescription)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(problem => problem.OutputDescription)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(problem => problem.TimeLimitMs)
            .IsRequired();

        builder.Property(problem => problem.MemoryLimitMb)
            .IsRequired();

        builder.Property(problem => problem.IsPublished)
            .IsRequired();

        builder.Property(problem => problem.JudgeMode)
            .HasConversion<int>()
            .IsRequired()
            .HasDefaultValue(JudgeMode.StandardInputOutput);

        builder.Property(problem => problem.FunctionSpecJson)
            .HasColumnType("text");

        builder.Property(problem => problem.StarterCodeJson)
            .HasColumnType("text");

        builder.Property(problem => problem.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(problem => problem.DeletedAt);

        builder.Property(problem => problem.CreatedByUserId)
            .IsRequired();

        builder.Property(problem => problem.CreatedAt)
            .IsRequired();

        builder.Property(problem => problem.UpdatedAt)
            .IsRequired();

        builder.HasMany(problem => problem.TestCases)
            .WithOne(testCase => testCase.Problem)
            .HasForeignKey(testCase => testCase.ProblemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(problem => problem.Submissions)
            .WithOne(submission => submission.Problem)
            .HasForeignKey(submission => submission.ProblemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
