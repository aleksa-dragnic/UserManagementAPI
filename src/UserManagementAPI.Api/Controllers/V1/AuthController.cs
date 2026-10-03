using Asp.Versioning;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

using UserManagementAPI.Api.Contracts.V1;
using UserManagementAPI.Api.Extensions;
using UserManagementAPI.Api.Hateoas;
using UserManagementAPI.Api.Mapping;
using UserManagementAPI.Api.Services;
using UserManagementAPI.Application.Abstractions;
using UserManagementAPI.Application.Auth;
using UserManagementAPI.Application.Auth.Commands.Logout;
using UserManagementAPI.Application.Auth.Commands.RefreshToken;
using UserManagementAPI.Domain.Common;

namespace UserManagementAPI.Api.Controllers.V1;

/// <summary>
/// Credentials in, tokens out. Anonymous by definition — a caller who could
/// already authenticate would not be here, and a caller presenting a refresh
/// token is authenticating with it.
///
/// The refresh token never appears in a body. Login and refresh set it as the
/// umapi_rt cookie, and refresh and logout read it from there (ADR 0019).
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/auth")]
[Produces("application/json")]
[AllowAnonymous]
[EnableRateLimiting(RateLimitingExtensions.AuthPolicy)]
public sealed class AuthController(IDispatcher dispatcher) : ControllerBase
{
    /// <summary>
    /// Exchanges an email and password for an access token. The refresh token
    /// is set as an HttpOnly cookie scoped to the auth endpoints.
    /// </summary>
    [HttpPost("login", Name = RouteNames.Login)]
    [ProducesResponseType<TokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await dispatcher.SendAsync(request.ToCommand(), cancellationToken);

        if (result.IsFailure)
        {
            return result.ToProblem(HttpContext);
        }

        RefreshTokenCookie.Write(Response, result.Value);

        return Ok(result.Value.ToResponse());
    }

    /// <summary>
    /// Exchanges the refresh cookie for a new access token and a new cookie.
    /// The presented token is retired. Presenting a token that was already
    /// exchanged revokes every session for the account. A refused token clears
    /// the cookie; losing the race against a simultaneous refresh (409) leaves
    /// it alone, because the winner's answer may already have replaced it.
    /// </summary>
    [HttpPost("refresh")]
    [ProducesResponseType<TokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        if (!RefreshTokenCookie.TryRead(Request, out var refreshToken))
        {
            return Result.Failure(AuthErrors.InvalidRefreshToken).ToProblem(HttpContext);
        }

        var result = await dispatcher.SendAsync(new RefreshTokenCommand(refreshToken), cancellationToken);

        if (result.IsFailure)
        {
            RefreshTokenCookie.Clear(Response);

            return result.ToProblem(HttpContext);
        }

        RefreshTokenCookie.Write(Response, result.Value);

        return Ok(result.Value.ToResponse());
    }

    /// <summary>
    /// Revokes the refresh token in the cookie and clears the cookie.
    /// Idempotent: no cookie, or a token already revoked, is a logout that has
    /// already happened.
    /// </summary>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        if (RefreshTokenCookie.TryRead(Request, out var refreshToken))
        {
            var result = await dispatcher.SendAsync(new LogoutCommand(refreshToken), cancellationToken);

            if (result.IsFailure)
            {
                return result.ToProblem(HttpContext);
            }
        }

        RefreshTokenCookie.Clear(Response);

        return NoContent();
    }
}