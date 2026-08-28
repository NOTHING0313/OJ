using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineJudge.Domain.Entities;

namespace OnlineJudge.Infrastructure.Persistence.Configurations;

public class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    public void Configure(EntityTypeBuilder<Team> builder)
    {
        builder.ToTable("Teams");
        builder.HasKey(team => team.Id);
        builder.HasIndex(team => team.NormalizedName).IsUnique().HasFilter("\"IsDeleted\" = FALSE");
        builder.Property(team => team.Name).HasMaxLength(40).IsRequired();
        builder.Property(team => team.NormalizedName).HasMaxLength(40).IsRequired();
        builder.Property(team => team.Description).HasMaxLength(500);
        builder.Property(team => team.IsDeleted).HasDefaultValue(false).IsRequired();
        builder.Property(team => team.CreatedAt).IsRequired();
        builder.Property(team => team.UpdatedAt).IsRequired();
        builder.HasOne(team => team.OwnerUser)
            .WithMany(user => user.OwnedTeams)
            .HasForeignKey(team => team.OwnerUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
