using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineJudge.Domain.Entities;

namespace OnlineJudge.Infrastructure.Persistence.Configurations;

public class ProblemJudgeAssetConfiguration : IEntityTypeConfiguration<ProblemJudgeAsset>
{
    public void Configure(EntityTypeBuilder<ProblemJudgeAsset> builder)
    {
        builder.ToTable("ProblemJudgeAssets");

        builder.HasKey(asset => asset.Id);

        builder.HasIndex(asset => new { asset.ProblemId, asset.Language, asset.NormalizedFileName })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = FALSE");

        builder.HasIndex(asset => asset.StoredFileName)
            .IsUnique();

        builder.Property(asset => asset.Language)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(asset => asset.OriginalFileName)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(asset => asset.NormalizedFileName)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(asset => asset.StoredFileName)
            .HasMaxLength(80)
            .IsRequired();

        builder.Property(asset => asset.StorageRelativePath)
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(asset => asset.Sha256)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(asset => asset.FileSizeBytes)
            .IsRequired();

        builder.Property(asset => asset.CreatedAt)
            .IsRequired();

        builder.Property(asset => asset.UpdatedAt)
            .IsRequired();

        builder.Property(asset => asset.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(asset => asset.DeletedAt);

        builder.HasOne(asset => asset.Problem)
            .WithMany(problem => problem.JudgeAssets)
            .HasForeignKey(asset => asset.ProblemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
