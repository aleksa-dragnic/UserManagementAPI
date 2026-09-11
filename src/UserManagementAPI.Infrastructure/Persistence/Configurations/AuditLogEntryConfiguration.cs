using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using UserManagementAPI.Infrastructure.Auditing;

namespace UserManagementAPI.Infrastructure.Persistence.Configurations;

internal sealed class AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> builder)
    {
        builder.ToTable("audit_log");

        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.Id).UseIdentityByDefaultColumn();

        builder.Property(entry => entry.Action).HasMaxLength(20).IsRequired();
        builder.Property(entry => entry.Entity).HasMaxLength(100).IsRequired();
        builder.Property(entry => entry.EntityId).HasMaxLength(200).IsRequired();
        builder.Property(entry => entry.Payload).HasColumnType("jsonb").IsRequired();
        builder.Property(entry => entry.OccurredAtUtc).IsRequired();

        // "What happened to this record" and "what happened around then" are
        // the two questions an audit log gets asked.
        builder.HasIndex(entry => new { entry.Entity, entry.EntityId });
        builder.HasIndex(entry => entry.OccurredAtUtc);
    }
}