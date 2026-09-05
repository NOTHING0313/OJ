using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineJudge.Domain.Entities;

namespace OnlineJudge.Infrastructure.Persistence.Configurations;

public class ProblemJudgeRevisionConfiguration : IEntityTypeConfiguration<ProblemJudgeRevision>
{
    public void Configure(EntityTypeBuilder<ProblemJudgeRevision> builder)
    {
        builder.ToTable("ProblemJudgeRevisions", table => table.HasCheckConstraint(
            "CK_ProblemJudgeRevisions_KindConfiguration",
            "(\"ProblemKind\" = 1 AND \"JudgeMode\" IN (1, 2) AND \"TimeLimitMs\" IS NOT NULL AND \"MemoryLimitMb\" IS NOT NULL) OR (\"ProblemKind\" = 2 AND \"JudgeMode\" IS NULL AND \"TimeLimitMs\" IS NULL AND \"MemoryLimitMb\" IS NULL AND \"AllowedLanguagesMask\" = 0 AND \"FunctionSpecJson\" IS NULL)"));
        builder.HasKey(revision => revision.Id);
        builder.HasIndex(revision => new { revision.ProblemId, revision.RevisionNumber }).IsUnique();

        builder.Property(revision => revision.ProblemKind).HasConversion<int>().IsRequired().HasDefaultValue(Domain.Enums.ProblemKind.Programming);
        builder.Property(revision => revision.JudgeMode).HasConversion<int?>();
        builder.Property(revision => revision.AllowedLanguagesMask).IsRequired();
        builder.Property(revision => revision.FunctionSpecJson).HasColumnType("text");
        builder.Property(revision => revision.TimeLimitMs);
        builder.Property(revision => revision.MemoryLimitMb);
        builder.Property(revision => revision.CreatedAt).IsRequired();

        builder.HasOne(revision => revision.Problem)
            .WithMany(problem => problem.JudgeRevisions)
            .HasForeignKey(revision => revision.ProblemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
