using UserManagementAPI.Domain.Common;

namespace UserManagementAPI.Domain.Users;

/// <summary>
/// Lifecycle state of a user account. Ids are stable — they are what the
/// status_id column stores, so renumbering would rewrite history.
/// </summary>
public sealed class UserStatus : Enumeration
{
    public static readonly UserStatus Pending = new(1, nameof(Pending));
    public static readonly UserStatus Active = new(2, nameof(Active));
    public static readonly UserStatus Locked = new(3, nameof(Locked));
    public static readonly UserStatus Deactivated = new(4, nameof(Deactivated));

    private UserStatus(int id, string name) : base(id, name)
    {
    }
}