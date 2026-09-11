namespace UserManagementAPI.Domain.Roles;

/// <summary>
/// The permission codes the API ships with. A code is the unit of authorization:
/// the seed data grants them to roles, the token carries them as claims, and the
/// policies check them. All three read these constants — a typo between any two
/// is an endpoint nobody can reach.
///
/// Moved here from Infrastructure in M4 PR16: the Api's policies need them, and
/// Api does not reference Infrastructure outside Program.cs.
/// </summary>
public static class PermissionCodes
{
    public const string UsersRead = "users.read";
    public const string UsersWrite = "users.write";
    public const string UsersLock = "users.lock";
    public const string RolesRead = "roles.read";
    public const string RolesManage = "roles.manage";

    public static readonly string[] All =
    [
        UsersRead,
        UsersWrite,
        UsersLock,
        RolesRead,
        RolesManage
    ];

    /// <summary>What a Member may do: look, not touch.</summary>
    public static readonly string[] ReadOnly =
    [
        UsersRead,
        RolesRead
    ];
}