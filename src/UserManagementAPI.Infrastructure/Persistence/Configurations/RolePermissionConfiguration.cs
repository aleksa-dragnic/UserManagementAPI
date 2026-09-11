using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using UserManagementAPI.Domain.Roles;

namespace UserManagementAPI.Infrastructure.Persistence.Configurations;

internal sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("role_permissions");

        builder.HasKey(rolePermission => rolePermission.Id);

        // Ids are assigned by the domain (Entity constructor), never by EF Core or
        // the database. Without this EF treats the Guid key as store-generated and
        // tracks a child discovered through a navigation, with its key already
        // set, as Modified instead of Added - the UPDATE then affects zero rows.
        builder.Property(rolePermission => rolePermission.Id).ValueGeneratedNever();

        builder.Property(rolePermission => rolePermission.RoleId).IsRequired();
        builder.Property(rolePermission => rolePermission.PermissionId).IsRequired();

        builder.HasIndex(rolePermission => new { rolePermission.RoleId, rolePermission.PermissionId })
            .IsUnique();

        // Restrict, not Cascade: deleting a permission code that roles still hold
        // should fail loudly rather than silently widen or narrow access.
        builder.HasOne<Permission>()
            .WithMany()
            .HasForeignKey(rolePermission => rolePermission.PermissionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}