using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineJudge.Domain.Entities;

namespace OnlineJudge.Infrastructure.Persistence.Configurations;

public sealed class SecurityAuditLogConfiguration : IEntityTypeConfiguration<SecurityAuditLog>
{
    public void Configure(EntityTypeBuilder<SecurityAuditLog> builder)
    {
        builder.ToTable("SecurityAuditLogs");
        builder.HasKey(log => log.Id);
        builder.HasIndex(log => log.CreatedAt).IsDescending();
        builder.HasIndex(log => new { log.ActorUserId, log.CreatedAt }).IsDescending(false, true);
        builder.HasIndex(log => new { log.Action, log.CreatedAt }).IsDescending(false, true);
        builder.HasIndex(log => new { log.TargetType, log.TargetId, log.CreatedAt }).IsDescending(false, false, true);
        builder.Property(log => log.ActorNameSnapshot).HasMaxLength(100);
        builder.Property(log => log.Action).HasMaxLength(128).IsRequired();
        builder.Property(log => log.TargetType).HasMaxLength(64).IsRequired();
        builder.Property(log => log.TargetId).HasMaxLength(256);
        builder.Property(log => log.Result).HasMaxLength(32).IsRequired();
        builder.Property(log => log.MetadataJson).HasColumnType("jsonb");
        builder.Property(log => log.CreatedAt).IsRequired();
        builder.Property(log => log.ClientIp).HasMaxLength(64);
        builder.HasOne<User>().WithMany().HasForeignKey(log => log.ActorUserId).OnDelete(DeleteBehavior.SetNull);
    }
}
