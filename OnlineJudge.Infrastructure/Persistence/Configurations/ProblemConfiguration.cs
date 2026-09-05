using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineJudge.Domain.Entities;
using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Infrastructure.Persistence.Configurations;

public class ProblemConfiguration : IEntityTypeConfiguration<Problem>
{
    public void Configure(EntityTypeBuilder<Problem> builder)
    {
        builder.ToTable("Problems", table =>
        {
            table.HasCheckConstraint("CK_Problems_AuthoringVersion", "\"AuthoringVersion\" >= 1");
            table.HasCheckConstraint("CK_Problems_KindConfiguration", "(\"ProblemKind\" = 1 AND \"JudgeMode\" IN (1, 2) AND \"TimeLimitMs\" IS NOT NULL AND \"MemoryLimitMb\" IS NOT NULL AND \"ChoiceAnswerRevealPolicy\" IS NULL AND \"ChoiceAnswerRevealAt\" IS NULL) OR (\"ProblemKind\" = 2 AND \"JudgeMode\" IS NULL AND \"TimeLimitMs\" IS NULL AND \"MemoryLimitMb\" IS NULL AND \"AllowedLanguagesMask\" = 0 AND \"FunctionSpecJson\" IS NULL AND \"StarterCodeJson\" IS NULL AND ((\"ChoiceAnswerRevealPolicy\" IS NULL AND \"ChoiceAnswerRevealAt\" IS NULL) OR (\"ChoiceAnswerRevealPolicy\" = 1 AND \"ChoiceAnswerRevealAt\" IS NULL) OR (\"ChoiceAnswerRevealPolicy\" = 2 AND \"ChoiceAnswerRevealAt\" IS NOT NULL)))");
        });

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

        builder.Property(problem => problem.ProblemKind)
            .HasConversion<int>()
            .IsRequired()
            .HasDefaultValue(ProblemKind.Programming);

        builder.Property(problem => problem.AuthoringVersion)
            .IsRequired()
            .HasDefaultValue(1L)
            .IsConcurrencyToken();

        builder.Property(problem => problem.TimeLimitMs);

        builder.Property(problem => problem.MemoryLimitMb);

        builder.Property(problem => problem.IsPublished)
            .IsRequired();

        builder.Property(problem => problem.JudgeMode)
            .HasConversion<int?>();

        builder.Property(problem => problem.AllowedLanguagesMask)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(problem => problem.FunctionSpecJson)
            .HasColumnType("text");

        builder.Property(problem => problem.StarterCodeJson)
            .HasColumnType("text");

        builder.Property(problem => problem.ChoiceAnswerRevealPolicy).HasConversion<int?>();
        builder.Property(problem => problem.ChoiceAnswerRevealAt);

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

        builder.HasOne(problem => problem.CurrentJudgeRevision)
            .WithMany()
            .HasForeignKey(problem => problem.CurrentJudgeRevisionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(problem => problem.TestCases)
            .WithOne(testCase => testCase.Problem)
            .HasForeignKey(testCase => testCase.ProblemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(problem => problem.Submissions)
            .WithOne(submission => submission.Problem)
            .HasForeignKey(submission => submission.ProblemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(problem => problem.ChoiceQuestions)
            .WithOne(question => question.Problem)
            .HasForeignKey(question => question.ProblemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
