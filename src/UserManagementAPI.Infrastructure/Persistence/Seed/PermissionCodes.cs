namespace UserManagementAPI.Infrastructure.Persistence.Seed;

/// <summary>
/// The permission codes the API ships with. These strings are the contract
/// between the seed data and the authorization policies in M4 — a typo here is
/// an endpoint nobody can reach, so both sides read the same constants.
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