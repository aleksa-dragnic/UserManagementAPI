using UserManagementAPI.Domain.Common;

namespace UserManagementAPI.Domain.Roles;

/// <summary>
/// Links a role to a permission. Both sit inside the Role aggregate, so this
/// one does hold ids on either side rather than navigation properties — the
/// same rule as UserRole, applied within a boundary rather than across one.
/// </summary>
public sealed class RolePermission : Entity
{
    internal RolePermission(Guid roleId, Guid permissionId)
        : base(Guid.NewGuid())
    {
        RoleId = roleId;
        PermissionId = permissionId;
    }

    private RolePermission()
    {
    }

    public Guid RoleId { get; private set; }

    public Guid PermissionId { get; private set; }
}