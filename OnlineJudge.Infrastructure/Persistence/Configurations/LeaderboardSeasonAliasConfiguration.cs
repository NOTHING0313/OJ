using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineJudge.Domain.Entities;

namespace OnlineJudge.Infrastructure.Persistence.Configurations;

public class LeaderboardSeasonAliasConfiguration : IEntityTypeConfiguration<LeaderboardSeasonAlias>
{
    public void Configure(EntityTypeBuilder<LeaderboardSeasonAlias> builder)
    {
        builder.ToTable("LeaderboardSeasonAliases");
        builder.HasKey(alias => alias.Id);
        builder.HasIndex(alias => new { alias.SeasonId, alias.UserId }).IsUnique();
        builder.HasIndex(alias => new { alias.SeasonId, alias.Alias }).IsUnique();
        builder.Property(alias => alias.Alias).HasMaxLength(11).IsRequired();
        builder.Property(alias => alias.CreatedAt).IsRequired();

        builder.HasOne(alias => alias.Season)
            .WithMany(season => season.Aliases)
            .HasForeignKey(alias => alias.SeasonId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(alias => alias.User)
            .WithMany()
            .HasForeignKey(alias => alias.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
