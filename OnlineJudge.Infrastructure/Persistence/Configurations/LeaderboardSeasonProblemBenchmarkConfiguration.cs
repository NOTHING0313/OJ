using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineJudge.Domain.Entities;

namespace OnlineJudge.Infrastructure.Persistence.Configurations;

public sealed class LeaderboardSeasonProblemBenchmarkConfiguration : IEntityTypeConfiguration<LeaderboardSeasonProblemBenchmark>
{
    public void Configure(EntityTypeBuilder<LeaderboardSeasonProblemBenchmark> builder)
    {
        builder.ToTable("LeaderboardSeasonProblemBenchmarks", table =>
        {
            table.HasCheckConstraint("CK_LeaderboardSeasonProblemBenchmarks_Runtime", "\"RuntimeBaselineMs\" > 0");
            table.HasCheckConstraint("CK_LeaderboardSeasonProblemBenchmarks_Memory", "\"MemoryBaselineKb\" > 0");
        });
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => new { item.SeasonProblemId, item.Language }).IsUnique();
        builder.Property(item => item.Language).HasConversion<int>().IsRequired();
        builder.Property(item => item.RuntimeBaselineMs).IsRequired();
        builder.Property(item => item.MemoryBaselineKb).IsRequired();
        builder.Property(item => item.CreatedAt).IsRequired();
        builder.Property(item => item.UpdatedAt).IsRequired();
        builder.HasOne(item => item.SeasonProblem)
            .WithMany(problem => problem.Benchmarks)
            .HasForeignKey(item => item.SeasonProblemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
