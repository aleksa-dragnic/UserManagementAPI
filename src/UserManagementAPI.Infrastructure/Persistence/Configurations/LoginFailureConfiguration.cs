using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using UserManagementAPI.Infrastructure.Identity;

namespace UserManagementAPI.Infrastructure.Persistence.Configurations;

internal sealed class LoginFailureConfiguration : IEntityTypeConfiguration<LoginFailure>
{
    public void Configure(EntityTypeBuilder<LoginFailure> builder)
    {
        builder.ToTable("login_failures");

        // The key is what makes INSERT … ON CONFLICT DO UPDATE an atomic counter.
        builder.HasKey(failure => failure.EmailHash);
        builder.Property(failure => failure.EmailHash).HasMaxLength(64);

        // The cleanup deletes by age.
        builder.HasIndex(failure => failure.LastFailureAtUtc)
            .HasDatabaseName("ix_login_failures_last_failure_at_utc");
    }
}
