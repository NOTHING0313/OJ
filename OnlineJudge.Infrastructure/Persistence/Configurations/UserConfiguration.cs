using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineJudge.Domain.Entities;
using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(user => user.Id);

        builder.HasIndex(user => user.UserName)
            .IsUnique();

        builder.HasIndex(user => user.Email)
            .IsUnique();

        builder.Property(user => user.UserName)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(user => user.Email)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(user => user.PasswordHash)
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(user => user.AvatarUrl)
            .HasMaxLength(1024);

        builder.Property(user => user.PhoneNumber)
            .HasMaxLength(32);

        builder.HasIndex(user => user.PhoneNumber)
            .IsUnique()
            .HasFilter("\"PhoneNumber\" IS NOT NULL");

        builder.Property(user => user.PhoneNumberConfirmed)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(user => user.Role)
            .HasConversion<int>()
            .HasDefaultValue(UserRole.Answerer)
            .IsRequired();

        builder.Property(user => user.IsBlacklisted)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(user => user.IsLeaderboardAnonymous)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(user => user.IsDeleted)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(user => user.DeletedAt);

        builder.Property(user => user.ActiveSessionId);

        builder.Property(user => user.ActiveSessionIssuedAt);

        builder.Property(user => user.CreatedAt)
            .IsRequired();

        builder.Property(user => user.UpdatedAt)
            .IsRequired();

        builder.HasMany(user => user.Submissions)
            .WithOne(submission => submission.User)
            .HasForeignKey(submission => submission.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
