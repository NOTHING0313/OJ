using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineJudge.Domain.Entities;

namespace OnlineJudge.Infrastructure.Persistence.Configurations;

public class ChallengeTaskAnswerConfiguration : IEntityTypeConfiguration<ChallengeTaskAnswer>
{
    public void Configure(EntityTypeBuilder<ChallengeTaskAnswer> builder)
    {
        builder.ToTable("ChallengeTaskAnswers");

        builder.HasKey(answer => answer.Id);

        builder.HasIndex(answer => new { answer.UserId, answer.ChallengeTaskId })
            .IsUnique();

        builder.Property(answer => answer.Content)
            .HasMaxLength(10000)
            .IsRequired();

        builder.Property(answer => answer.CreatedAt)
            .IsRequired();

        builder.Property(answer => answer.UpdatedAt)
            .IsRequired();

        builder.HasOne(answer => answer.Challenge)
            .WithMany(challenge => challenge.Answers)
            .HasForeignKey(answer => answer.ChallengeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(answer => answer.ChallengeTask)
            .WithMany(task => task.Answers)
            .HasForeignKey(answer => answer.ChallengeTaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(answer => answer.User)
            .WithMany()
            .HasForeignKey(answer => answer.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
