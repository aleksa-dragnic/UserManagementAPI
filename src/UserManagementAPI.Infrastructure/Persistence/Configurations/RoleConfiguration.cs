using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using UserManagementAPI.Domain.Roles;

namespace UserManagementAPI.Infrastructure.Persistence.Configurations;

internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("roles");

        builder.HasKey(role => role.Id);

        // Ids are assigned by the domain (Entity constructor), never by EF Core or
        // the database. Without this EF treats the Guid key as store-generated and
        // tracks a child discovered through a navigation, with its key already
        // set, as Modified instead of Added - the UPDATE then affects zero rows.
        builder.Property(role => role.Id).ValueGeneratedNever();

        builder.Property(role => role.Name)
            .HasMaxLength(Role.MaxNameLength)
            .IsRequired();

        builder.HasIndex(role => role.Name).IsUnique();

        builder.HasMany(role => role.Permissions)
            .WithOne()
            .HasForeignKey(rolePermission => rolePermission.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata
            .FindNavigation(nameof(Role.Permissions))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(role => role.DomainEvents);
    }
}