using Microsoft.AspNetCore.Authorization;

namespace UserManagementAPI.Api.Authorization;

/// <summary>The caller must hold this permission code.</summary>
public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}