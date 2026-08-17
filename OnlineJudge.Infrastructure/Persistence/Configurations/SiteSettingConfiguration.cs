using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineJudge.Domain.Entities;

namespace OnlineJudge.Infrastructure.Persistence.Configurations;

public class SiteSettingConfiguration : IEntityTypeConfiguration<SiteSetting>
{
    public void Configure(EntityTypeBuilder<SiteSetting> builder)
    {
        builder.ToTable("SiteSettings");

        builder.HasKey(setting => setting.Id);

        builder.HasIndex(setting => setting.Key)
            .IsUnique();

        builder.Property(setting => setting.Key)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(setting => setting.Value)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(setting => setting.UpdatedAt)
            .IsRequired();

        builder.Property(setting => setting.UpdatedByUserId);
    }
}
