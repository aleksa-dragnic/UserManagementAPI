using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using UserManagementAPI.Domain.Roles;

namespace UserManagementAPI.Infrastructure.Persistence.Configurations;

internal sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("permissions");

        builder.HasKey(permission => permission.Id);

        // Ids are assigned by the domain (Entity constructor), never by EF Core or
        // the database. Without this EF treats the Guid key as store-generated and
        // tracks a child discovered through a navigation, with its key already
        // set, as Modified instead of Added - the UPDATE then affects zero rows.
        builder.Property(permission => permission.Id).ValueGeneratedNever();

        builder.Property(permission => permission.Code)
            .HasMaxLength(Permission.MaxCodeLength)
            .IsRequired();

        builder.HasIndex(permission => permission.Code).IsUnique();
    }
}