using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineJudge.Domain.Entities;

namespace OnlineJudge.Infrastructure.Persistence.Configurations;

public class ProblemCollaboratorConfiguration : IEntityTypeConfiguration<ProblemCollaborator>
{
    public void Configure(EntityTypeBuilder<ProblemCollaborator> builder)
    {
        builder.ToTable("ProblemCollaborators");

        builder.HasKey(collaborator => collaborator.Id);

        builder.HasIndex(collaborator => new { collaborator.ProblemId, collaborator.UserId })
            .IsUnique();

        builder.Property(collaborator => collaborator.CanEditProblem)
            .IsRequired();

        builder.Property(collaborator => collaborator.CanManageTestCases)
            .IsRequired();

        builder.Property(collaborator => collaborator.CreatedAt)
            .IsRequired();

        builder.HasOne(collaborator => collaborator.Problem)
            .WithMany(problem => problem.Collaborators)
            .HasForeignKey(collaborator => collaborator.ProblemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(collaborator => collaborator.User)
            .WithMany(user => user.ProblemCollaborations)
            .HasForeignKey(collaborator => collaborator.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(collaborator => collaborator.GrantedByUser)
            .WithMany(user => user.GrantedProblemCollaborations)
            .HasForeignKey(collaborator => collaborator.GrantedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
