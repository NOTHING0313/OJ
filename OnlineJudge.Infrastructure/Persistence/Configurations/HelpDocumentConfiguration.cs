using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineJudge.Domain.Entities;

namespace OnlineJudge.Infrastructure.Persistence.Configurations;

public sealed class HelpDocumentConfiguration : IEntityTypeConfiguration<HelpDocument>
{
    public void Configure(EntityTypeBuilder<HelpDocument> builder)
    {
        builder.ToTable("HelpDocuments");
        builder.HasKey(document => document.Id);
        builder.HasIndex(document => document.Slug).IsUnique();
        builder.HasIndex(document => new { document.IsPublished, document.SortOrder });
        builder.Property(document => document.Title).HasMaxLength(120).IsRequired();
        builder.Property(document => document.Slug).HasMaxLength(120).IsRequired();
        builder.Property(document => document.Summary).HasMaxLength(300);
        builder.Property(document => document.MarkdownContent).HasColumnType("text").IsRequired();
        builder.Property(document => document.IsPublished).IsRequired();
        builder.Property(document => document.SortOrder).IsRequired();
        builder.Property(document => document.CreatedAt).IsRequired();
        builder.Property(document => document.UpdatedAt).IsRequired();
        builder.HasOne<User>().WithMany().HasForeignKey(document => document.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(document => document.UpdatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
