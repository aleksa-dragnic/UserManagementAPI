using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using UserManagementAPI.Infrastructure.Idempotency;

namespace UserManagementAPI.Infrastructure.Persistence.Configurations;

internal sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("idempotency_keys");

        // The scope is the key: one caller, one operation, one key. The primary
        // key's uniqueness is what makes INSERT … ON CONFLICT DO NOTHING a claim.
        builder.HasKey(record => new { record.CallerId, record.Operation, record.Key });

        builder.Property(record => record.Operation).HasMaxLength(200);
        builder.Property(record => record.Key).HasMaxLength(IdempotencyRecord.MaxKeyLength);
        builder.Property(record => record.RequestHash).HasMaxLength(64).IsRequired();
        builder.Property(record => record.Status).HasMaxLength(20).IsRequired();
        builder.Property(record => record.ResponseLocation).HasMaxLength(2000);
        builder.Property(record => record.ResponseContentType).HasMaxLength(200);

        // No foreign key to users: a key outlives nothing it refers to, and the
        // cleanup removes it within the retention period anyway.

        // The cleanup deletes by expiry.
        builder.HasIndex(record => record.ExpiresAtUtc)
            .HasDatabaseName("ix_idempotency_keys_expires_at_utc");
    }
}
