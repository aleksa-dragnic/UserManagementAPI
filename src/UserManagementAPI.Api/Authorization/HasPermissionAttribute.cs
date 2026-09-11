using Microsoft.AspNetCore.Authorization;

namespace UserManagementAPI.Api.Authorization;

/// <summary>
/// [HasPermission("users.write")] — the only authorization attribute used on an
/// action. A bare [Authorize] says "someone"; this says who.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class HasPermissionAttribute(string permission)
    : AuthorizeAttribute(PermissionPolicyProvider.Prefix + permission)
{
    public string Permission { get; } = permission;
}