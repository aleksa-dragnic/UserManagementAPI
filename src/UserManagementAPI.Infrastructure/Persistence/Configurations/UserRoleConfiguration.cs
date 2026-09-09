using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using UserManagementAPI.Domain.Roles;
using UserManagementAPI.Domain.Users;

namespace UserManagementAPI.Infrastructure.Persistence.Configurations;

internal sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("user_roles");

        // The plan calls for a composite key on (UserId, RoleId). UserRole derives
        // from Entity, which owns a Guid Id and defines equality on it, so the key
        // stays on Id and the pair is enforced by a unique index instead. Same
        // guarantee, without breaking the entity contract.
        builder.HasKey(userRole => userRole.Id);

        builder.Property(userRole => userRole.UserId).IsRequired();
        builder.Property(userRole => userRole.RoleId).IsRequired();
        builder.Property(userRole => userRole.AssignedAtUtc).IsRequired();

        builder.HasIndex(userRole => new { userRole.UserId, userRole.RoleId }).IsUnique();

        // A foreign key to roles, but no navigation property: Role is a separate
        // aggregate and the reference stays an id. Restrict, as with
        // role_permissions - deleting a role users still hold should fail loudly.
        builder.HasOne<Role>()
            .WithMany()
            .HasForeignKey(userRole => userRole.RoleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}