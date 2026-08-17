using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineJudge.Domain.Entities;

namespace OnlineJudge.Infrastructure.Persistence.Configurations;

public class ChallengeTaskConfiguration : IEntityTypeConfiguration<ChallengeTask>
{
    public void Configure(EntityTypeBuilder<ChallengeTask> builder)
    {
        builder.ToTable("ChallengeTasks", table =>
        {
            table.HasCheckConstraint("CK_ChallengeTasks_BoardX", "\"BoardX\" >= 0 AND \"BoardX\" <= 7");
            table.HasCheckConstraint("CK_ChallengeTasks_BoardY", "\"BoardY\" >= 0 AND \"BoardY\" <= 7");
        });

        builder.HasKey(task => task.Id);

        builder.HasIndex(task => new { task.ChallengeId, task.BoardX, task.BoardY })
            .IsUnique();

        builder.Property(task => task.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(task => task.Description)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(task => task.TaskType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(task => task.Difficulty)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(task => task.BoardX)
            .IsRequired();

        builder.Property(task => task.BoardY)
            .IsRequired();

        builder.Property(task => task.Score)
            .IsRequired();

        builder.Property(task => task.IsPublished)
            .IsRequired();

        builder.Property(task => task.CreatedAt)
            .IsRequired();

        builder.Property(task => task.UpdatedAt)
            .IsRequired();

        builder.HasOne(task => task.AlgorithmProblem)
            .WithMany(problem => problem.ChallengeTasks)
            .HasForeignKey(task => task.AlgorithmProblemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
