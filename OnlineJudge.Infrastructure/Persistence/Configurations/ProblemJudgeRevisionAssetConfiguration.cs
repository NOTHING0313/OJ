using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineJudge.Domain.Entities;

namespace OnlineJudge.Infrastructure.Persistence.Configurations;

public class ProblemJudgeRevisionAssetConfiguration : IEntityTypeConfiguration<ProblemJudgeRevisionAsset>
{
    public void Configure(EntityTypeBuilder<ProblemJudgeRevisionAsset> builder)
    {
        builder.ToTable("ProblemJudgeRevisionAssets");
        builder.HasKey(asset => new { asset.ProblemJudgeRevisionId, asset.ProblemJudgeAssetId });
        builder.HasIndex(asset => new { asset.ProblemJudgeRevisionId, asset.Order }).IsUnique();

        builder.HasOne(asset => asset.ProblemJudgeRevision)
            .WithMany(revision => revision.Assets)
            .HasForeignKey(asset => asset.ProblemJudgeRevisionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(asset => asset.ProblemJudgeAsset)
            .WithMany(source => source.JudgeRevisionAssets)
            .HasForeignKey(asset => asset.ProblemJudgeAssetId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
