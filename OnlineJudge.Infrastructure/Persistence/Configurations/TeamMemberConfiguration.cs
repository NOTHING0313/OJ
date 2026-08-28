using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineJudge.Domain.Entities;

namespace OnlineJudge.Infrastructure.Persistence.Configurations;

public class TeamMemberConfiguration : IEntityTypeConfiguration<TeamMember>
{
    public void Configure(EntityTypeBuilder<TeamMember> builder)
    {
        builder.ToTable("TeamMembers");
        builder.HasKey(member => member.Id);
        builder.HasIndex(member => member.UserId).IsUnique().HasFilter("\"IsActive\" = TRUE");
        builder.HasIndex(member => new { member.TeamId, member.UserId }).IsUnique().HasFilter("\"IsActive\" = TRUE");
        builder.Property(member => member.Role).HasConversion<int>().IsRequired();
        builder.Property(member => member.IsActive).HasDefaultValue(true).IsRequired();
        builder.Property(member => member.JoinedAt).IsRequired();
        builder.HasOne(member => member.Team)
            .WithMany(team => team.Members)
            .HasForeignKey(member => member.TeamId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(member => member.User)
            .WithMany(user => user.TeamMemberships)
            .HasForeignKey(member => member.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
