using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using UserManagementAPI.Api.Contracts.V1;
using UserManagementAPI.Api.Extensions;
using UserManagementAPI.Api.Mapping;
using UserManagementAPI.Application.Abstractions;

namespace UserManagementAPI.Api.Controllers.V1;

/// <summary>
/// Credentials in, tokens out. Anonymous by definition — a caller who could
/// already authenticate would not be here.
/// </summary>
[ApiController]
[Route("api/v1/auth")]
[Produces("application/json")]
[AllowAnonymous]
public sealed class AuthController(IDispatcher dispatcher) : ControllerBase
{
    /// <summary>Exchanges an email and password for an access token.</summary>
    [HttpPost("login")]
    [ProducesResponseType<TokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await dispatcher.SendAsync(request.ToCommand(), cancellationToken);

        return result.IsSuccess ? Ok(result.Value.ToResponse()) : result.ToProblem(HttpContext);
    }
}