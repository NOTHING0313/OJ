using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineJudge.Domain.Entities;

namespace OnlineJudge.Infrastructure.Persistence.Configurations;

public sealed class JudgeJobConfiguration : IEntityTypeConfiguration<JudgeJob>
{
    public void Configure(EntityTypeBuilder<JudgeJob> builder)
    {
        builder.ToTable("JudgeJobs", table =>
        {
            table.HasCheckConstraint("CK_JudgeJobs_AttemptCount", "\"AttemptCount\" >= 0");
            table.HasCheckConstraint("CK_JudgeJobs_Status", "\"Status\" BETWEEN 1 AND 4");
            table.HasCheckConstraint("CK_JudgeJobs_FailureKind", "\"LastFailureKind\" IS NULL OR \"LastFailureKind\" IN (1, 2)");
            table.HasCheckConstraint(
                "CK_JudgeJobs_LeaseState",
                "(\"Status\" = 1 AND \"LeaseToken\" IS NULL AND \"LeaseOwner\" IS NULL AND \"LeaseExpiresAt\" IS NULL AND \"FinishedAt\" IS NULL) OR " +
                "(\"Status\" = 2 AND \"LeaseToken\" IS NOT NULL AND \"LeaseOwner\" IS NOT NULL AND length(\"LeaseOwner\") > 0 AND \"LeaseExpiresAt\" IS NOT NULL AND \"FinishedAt\" IS NULL) OR " +
                "(\"Status\" IN (3, 4) AND \"LeaseToken\" IS NULL AND \"LeaseOwner\" IS NULL AND \"LeaseExpiresAt\" IS NULL AND \"FinishedAt\" IS NOT NULL)");
        });

        builder.HasKey(job => job.SubmissionId);

        builder.Property(job => job.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(job => job.AttemptCount)
            .IsRequired();

        builder.Property(job => job.AvailableAt)
            .IsRequired();

        builder.Property(job => job.LeaseOwner)
            .HasMaxLength(200);

        builder.Property(job => job.LastFailureKind)
            .HasConversion<int?>();

        builder.Property(job => job.LastError)
            .HasMaxLength(2048);

        builder.Property(job => job.CreatedAt)
            .IsRequired();

        builder.Property(job => job.UpdatedAt)
            .IsRequired();

        builder.HasIndex(job => new { job.Status, job.AvailableAt, job.CreatedAt, job.SubmissionId });
        builder.HasIndex(job => new { job.Status, job.LeaseExpiresAt, job.CreatedAt, job.SubmissionId });

        builder.HasOne(job => job.Submission)
            .WithOne(submission => submission.JudgeJob)
            .HasForeignKey<JudgeJob>(job => job.SubmissionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
