namespace UserManagementAPI.Api.Hateoas;

/// <summary>
/// Route names links are generated from. Unique across the application and
/// suffixed with the version, because every version of an action is a separate
/// route and two routes cannot share a name.
/// </summary>
public static class RouteNames
{
    public const string Root = "Root";

    public const string Login = "LoginV1";

    public const string GetUsers = "GetUsersV1";

    public const string GetUser = "GetUserV1";

    public const string RegisterUser = "RegisterUserV1";

    public const string UpdateUser = "UpdateUserV1";

    public const string LockUser = "LockUserV1";

    public const string UnlockUser = "UnlockUserV1";

    public const string AssignRole = "AssignRoleV1";

    public const string RemoveRole = "RemoveRoleV1";

    public const string GetRoles = "GetRolesV1";

    public const string GetRole = "GetRoleV1";
}