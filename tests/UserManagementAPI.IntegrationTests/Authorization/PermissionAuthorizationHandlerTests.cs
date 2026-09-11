using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.Options;

using UserManagementAPI.Api.Authorization;
using UserManagementAPI.Application.Abstractions;

namespace UserManagementAPI.IntegrationTests.Authorization;

/// <summary>No HTTP, no database: the handler and the policy provider in isolation.</summary>
public sealed class PermissionAuthorizationHandlerTests
{
    [Fact]
    public async Task Succeeds_WhenThePrincipalHoldsThePermissionClaim()
    {
        var context = Context(new PermissionRequirement("users.write"), "users.read", "users.write");

        await new PermissionAuthorizationHandler().HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task DoesNotSucceed_WhenTheClaimIsMissing()
    {
        var context = Context(new PermissionRequirement("users.write"), "users.read");

        await new PermissionAuthorizationHandler().HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task ThePolicyProvider_BuildsAPolicyFromThePrefixedName_AndDelegatesTheRest()
    {
        var provider = new PermissionPolicyProvider(Options.Create(new AuthorizationOptions()));

        var permissionPolicy = await provider.GetPolicyAsync(PermissionPolicyProvider.Prefix + "roles.manage");
        var unknownPolicy = await provider.GetPolicyAsync("something-else");

        permissionPolicy.Should().NotBeNull();
        permissionPolicy!.Requirements.OfType<PermissionRequirement>().Should().ContainSingle()
            .Which.Permission.Should().Be("roles.manage");
        permissionPolicy.Requirements.OfType<DenyAnonymousAuthorizationRequirement>().Should().ContainSingle();
        unknownPolicy.Should().BeNull();
    }

    private static AuthorizationHandlerContext Context(PermissionRequirement requirement, params string[] permissions)
    {
        var identity = new ClaimsIdentity(
            permissions.Select(permission => new Claim(AuthClaimTypes.Permission, permission)),
            authenticationType: "Test");

        return new AuthorizationHandlerContext([requirement], new ClaimsPrincipal(identity), resource: null);
    }
}