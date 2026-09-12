using Asp.Versioning;

using Microsoft.AspNetCore.Mvc;

using UserManagementAPI.Api.Authorization;
using UserManagementAPI.Api.Extensions;
using UserManagementAPI.Api.Filters;
using UserManagementAPI.Api.Mapping;
using UserManagementAPI.Application.Abstractions;
using UserManagementAPI.Application.Users.Queries.GetUserById;
using UserManagementAPI.Domain.Roles;

using V2Contracts = UserManagementAPI.Api.Contracts.V2;

namespace UserManagementAPI.Api.Controllers.V2;

/// <summary>
/// Version 2 of the users resource. Only the single-user read has a v2 shape;
/// every other route exists in v1 alone, and asking for it under v2 is an
/// unsupported version. Same class name as v1 on purpose: API versioning
/// collates controllers by name, so both report "1.0, 2.0" as supported.
/// </summary>
[ApiController]
[ApiVersion(2.0)]
[Route("api/v{version:apiVersion}/users")]
[Produces("application/json")]
public sealed class UsersController(IDispatcher dispatcher) : ControllerBase
{
    /// <summary>Returns one user with a single display name.</summary>
    [HttpGet("{id:guid}")]
    [HasPermission(PermissionCodes.UsersRead)]
    [ETagFilter]
    [ProducesResponseType<V2Contracts.UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUser(Guid id, CancellationToken cancellationToken)
    {
        var result = await dispatcher.QueryAsync(new GetUserByIdQuery(id), cancellationToken);

        return result.IsSuccess ? Ok(result.Value.ToV2Response()) : result.ToProblem(HttpContext);
    }
}