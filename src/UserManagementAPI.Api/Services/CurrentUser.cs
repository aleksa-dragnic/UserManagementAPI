using System.Security.Claims;

using Microsoft.IdentityModel.JsonWebTokens;

using UserManagementAPI.Application.Abstractions;

namespace UserManagementAPI.Api.Services;

/// <summary>
/// The caller, read from the validated bearer token. Registered before
/// AddInfrastructure so it takes precedence over the Infrastructure default,
/// which answers "nobody" for writes that happen outside a request.
/// </summary>
internal sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public Guid? UserId =>
        Guid.TryParse(httpContextAccessor.HttpContext?.User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id)
            ? id
            : null;

    public bool IsAuthenticated => httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated ?? false;
}