using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineJudge.Domain.Entities;

namespace OnlineJudge.Infrastructure.Persistence.Configurations;

public class ChallengeTaskFileSubmissionConfiguration : IEntityTypeConfiguration<ChallengeTaskFileSubmission>
{
    public void Configure(EntityTypeBuilder<ChallengeTaskFileSubmission> builder)
    {
        builder.ToTable("ChallengeTaskFileSubmissions");

        builder.HasKey(submission => submission.Id);

        builder.HasIndex(submission => new { submission.UserId, submission.ChallengeTaskId })
            .IsUnique();

        builder.Property(submission => submission.OriginalFileName)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(submission => submission.StoredFileName)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(submission => submission.FilePath)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(submission => submission.FileSizeBytes)
            .IsRequired();

        builder.Property(submission => submission.ContentType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(submission => submission.ReviewComment)
            .HasMaxLength(2000);

        builder.Property(submission => submission.CreatedAt)
            .IsRequired();

        builder.Property(submission => submission.UpdatedAt)
            .IsRequired();

        builder.HasOne(submission => submission.Challenge)
            .WithMany(challenge => challenge.FileSubmissions)
            .HasForeignKey(submission => submission.ChallengeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(submission => submission.ChallengeTask)
            .WithMany(task => task.FileSubmissions)
            .HasForeignKey(submission => submission.ChallengeTaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(submission => submission.User)
            .WithMany()
            .HasForeignKey(submission => submission.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(submission => submission.ReviewedByUser)
            .WithMany()
            .HasForeignKey(submission => submission.ReviewedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
