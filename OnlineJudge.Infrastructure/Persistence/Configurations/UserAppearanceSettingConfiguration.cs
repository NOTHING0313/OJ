using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineJudge.Domain.Entities;

namespace OnlineJudge.Infrastructure.Persistence.Configurations;

public class UserAppearanceSettingConfiguration : IEntityTypeConfiguration<UserAppearanceSetting>
{
    public void Configure(EntityTypeBuilder<UserAppearanceSetting> builder)
    {
        builder.ToTable("UserAppearanceSettings");

        builder.HasKey(setting => setting.Id);

        builder.HasIndex(setting => setting.UserId)
            .IsUnique();

        builder.Property(setting => setting.BackgroundImageUrl)
            .HasMaxLength(1024);

        builder.Property(setting => setting.BackgroundEnabled)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(setting => setting.PositionX)
            .HasPrecision(5, 2)
            .HasDefaultValue(50m)
            .IsRequired();

        builder.Property(setting => setting.PositionY)
            .HasPrecision(5, 2)
            .HasDefaultValue(50m)
            .IsRequired();

        builder.Property(setting => setting.Scale)
            .HasPrecision(4, 2)
            .HasDefaultValue(1m)
            .IsRequired();

        builder.Property(setting => setting.OverlayOpacity)
            .HasPrecision(4, 2)
            .HasDefaultValue(0.65m)
            .IsRequired();

        builder.Property(setting => setting.UpdatedAt)
            .IsRequired();

        builder.HasOne(setting => setting.User)
            .WithOne()
            .HasForeignKey<UserAppearanceSetting>(setting => setting.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
