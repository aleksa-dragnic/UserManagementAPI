using UserManagementAPI.Domain.Common;

namespace UserManagementAPI.Domain.Users;

/// <summary>
/// A role assignment inside the User aggregate. It holds a RoleId and never a
/// Role navigation property: Role is a separate aggregate, and crossing that
/// line with a navigation property is what collapses two aggregates into one
/// object graph.
/// </summary>
public sealed class UserRole : Entity
{
    internal UserRole(Guid userId, Guid roleId)
        : base(Guid.NewGuid())
    {
        UserId = userId;
        RoleId = roleId;
        AssignedAtUtc = DateTime.UtcNow;
    }

    private UserRole()
    {
    }

    public Guid UserId { get; private set; }

    public Guid RoleId { get; private set; }

    public DateTime AssignedAtUtc { get; private set; }
}