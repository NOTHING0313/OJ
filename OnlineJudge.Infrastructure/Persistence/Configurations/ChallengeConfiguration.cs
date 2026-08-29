using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineJudge.Domain.Entities;

namespace OnlineJudge.Infrastructure.Persistence.Configurations;

public class ChallengeConfiguration : IEntityTypeConfiguration<Challenge>
{
    public void Configure(EntityTypeBuilder<Challenge> builder)
    {
        builder.ToTable("Challenges");

        builder.HasKey(challenge => challenge.Id);

        builder.Property(challenge => challenge.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(challenge => challenge.Description)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(challenge => challenge.StartAt)
            .IsRequired();

        builder.Property(challenge => challenge.EndAt)
            .IsRequired();

        builder.Property(challenge => challenge.CreatedByUserId)
            .IsRequired();

        builder.Property(challenge => challenge.IsPublished)
            .IsRequired();

        builder.Property(challenge => challenge.ParticipationMode)
            .HasConversion<int>()
            .HasDefaultValue(OnlineJudge.Domain.Enums.ChallengeParticipationMode.Individual)
            .IsRequired();

        builder.Property(challenge => challenge.PeerReviewEnabled)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(challenge => challenge.PeerReviewEndAt);

        builder.Property(challenge => challenge.CreatedAt)
            .IsRequired();

        builder.Property(challenge => challenge.UpdatedAt)
            .IsRequired();

        builder.HasOne(challenge => challenge.CreatedByUser)
            .WithMany(user => user.CreatedChallenges)
            .HasForeignKey(challenge => challenge.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(challenge => challenge.Tasks)
            .WithOne(task => task.Challenge)
            .HasForeignKey(task => task.ChallengeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
