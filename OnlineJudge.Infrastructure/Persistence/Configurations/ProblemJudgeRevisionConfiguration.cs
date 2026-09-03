using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineJudge.Domain.Entities;

namespace OnlineJudge.Infrastructure.Persistence.Configurations;

public class ProblemJudgeRevisionConfiguration : IEntityTypeConfiguration<ProblemJudgeRevision>
{
    public void Configure(EntityTypeBuilder<ProblemJudgeRevision> builder)
    {
        builder.ToTable("ProblemJudgeRevisions");
        builder.HasKey(revision => revision.Id);
        builder.HasIndex(revision => new { revision.ProblemId, revision.RevisionNumber }).IsUnique();

        builder.Property(revision => revision.JudgeMode).HasConversion<int>().IsRequired();
        builder.Property(revision => revision.AllowedLanguagesMask).IsRequired();
        builder.Property(revision => revision.FunctionSpecJson).HasColumnType("text");
        builder.Property(revision => revision.TimeLimitMs).IsRequired();
        builder.Property(revision => revision.MemoryLimitMb).IsRequired();
        builder.Property(revision => revision.CreatedAt).IsRequired();

        builder.HasOne(revision => revision.Problem)
            .WithMany(problem => problem.JudgeRevisions)
            .HasForeignKey(revision => revision.ProblemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
