using Asp.Versioning;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using UserManagementAPI.Api.Contracts.V1;
using UserManagementAPI.Api.Extensions;
using UserManagementAPI.Api.Mapping;
using UserManagementAPI.Application.Abstractions;

namespace UserManagementAPI.Api.Controllers.V1;

/// <summary>
/// Credentials in, tokens out. Anonymous by definition — a caller who could
/// already authenticate would not be here, and a caller presenting a refresh
/// token is authenticating with it.
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/auth")]
[Produces("application/json")]
[AllowAnonymous]
public sealed class AuthController(IDispatcher dispatcher) : ControllerBase
{
    /// <summary>Exchanges an email and password for a token pair.</summary>
    [HttpPost("login")]
    [ProducesResponseType<TokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await dispatcher.SendAsync(request.ToCommand(), cancellationToken);

        return result.IsSuccess ? Ok(result.Value.ToResponse()) : result.ToProblem(HttpContext);
    }

    /// <summary>
    /// Exchanges a refresh token for a new pair. The presented token is retired.
    /// Presenting a token that was already exchanged revokes every session for
    /// the account.
    /// </summary>
    [HttpPost("refresh")]
    [ProducesResponseType<TokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Refresh(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var result = await dispatcher.SendAsync(request.ToRefreshCommand(), cancellationToken);

        return result.IsSuccess ? Ok(result.Value.ToResponse()) : result.ToProblem(HttpContext);
    }

    /// <summary>Revokes the presented refresh token. Idempotent.</summary>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Logout(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var result = await dispatcher.SendAsync(request.ToLogoutCommand(), cancellationToken);

        return result.IsSuccess ? NoContent() : result.ToProblem(HttpContext);
    }
}