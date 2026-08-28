using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineJudge.Domain.Entities;

namespace OnlineJudge.Infrastructure.Persistence.Configurations;

public class TeamProjectConfiguration : IEntityTypeConfiguration<TeamProject>
{
    public void Configure(EntityTypeBuilder<TeamProject> builder)
    {
        builder.ToTable("TeamProjects");
        builder.HasKey(project => project.Id);
        builder.HasIndex(project => new { project.TeamId, project.NormalizedName }).IsUnique();
        builder.HasIndex(project => new { project.TeamId, project.NormalizedRepositoryUrl }).IsUnique();
        builder.Property(project => project.Name).HasMaxLength(80).IsRequired();
        builder.Property(project => project.NormalizedName).HasMaxLength(80).IsRequired();
        builder.Property(project => project.RepositoryUrl).HasMaxLength(2048).IsRequired();
        builder.Property(project => project.NormalizedRepositoryUrl).HasMaxLength(2048).IsRequired();
        builder.Property(project => project.CreatedAt).IsRequired();
        builder.Property(project => project.UpdatedAt).IsRequired();
        builder.HasOne(project => project.Team)
            .WithMany(team => team.Projects)
            .HasForeignKey(project => project.TeamId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(project => project.CreatedByUser)
            .WithMany(user => user.CreatedTeamProjects)
            .HasForeignKey(project => project.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
