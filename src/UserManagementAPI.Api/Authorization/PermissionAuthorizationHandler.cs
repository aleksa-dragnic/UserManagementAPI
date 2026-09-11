using Microsoft.AspNetCore.Authorization;

using UserManagementAPI.Application.Abstractions;

namespace UserManagementAPI.Api.Authorization;

/// <summary>
/// Succeeds when the principal carries a "permission" claim with the required
/// code. The claims were put there at login from the user's roles; nothing is
/// looked up here, so the check costs nothing and works on any replica.
/// </summary>
public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context.User.HasClaim(AuthClaimTypes.Permission, requirement.Permission))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}