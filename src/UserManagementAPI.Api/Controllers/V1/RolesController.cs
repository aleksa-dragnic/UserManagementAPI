using Asp.Versioning;

using Microsoft.AspNetCore.Mvc;

using UserManagementAPI.Api.Authorization;
using UserManagementAPI.Api.Contracts.V1;
using UserManagementAPI.Api.Extensions;
using UserManagementAPI.Api.Filters;
using UserManagementAPI.Api.Hateoas;
using UserManagementAPI.Api.Mapping;
using UserManagementAPI.Application.Abstractions;
using UserManagementAPI.Application.Roles.Queries.GetRoleById;
using UserManagementAPI.Application.Roles.Queries.GetRoles;
using UserManagementAPI.Domain.Roles;

namespace UserManagementAPI.Api.Controllers.V1;

/// <summary>
/// The roles resource, read-only. Assigning a role takes its id, and this is
/// where a client finds one. Roles themselves are seeded; managing them over
/// HTTP is out of scope for v1.
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/roles")]
[Produces("application/json")]
public sealed class RolesController(IDispatcher dispatcher) : ControllerBase
{
    /// <summary>Lists roles with the permission codes each grants, ordered by name.</summary>
    [HttpGet(Name = RouteNames.GetRoles)]
    [HttpHead]
    [HasPermission(PermissionCodes.RolesRead)]
    [ETagFilter]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType<IReadOnlyList<RoleResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetRoles(CancellationToken cancellationToken)
    {
        var result = await dispatcher.QueryAsync(new GetRolesQuery(), cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value.Select(role => role.ToResponse()).ToList())
            : result.ToProblem(HttpContext);
    }

    /// <summary>Returns one role with its permission codes.</summary>
    [HttpGet("{id:guid}", Name = RouteNames.GetRole)]
    [HttpHead("{id:guid}")]
    [HasPermission(PermissionCodes.RolesRead)]
    [ETagFilter]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType<RoleResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRole(Guid id, CancellationToken cancellationToken)
    {
        var result = await dispatcher.QueryAsync(new GetRoleByIdQuery(id), cancellationToken);

        return result.IsSuccess ? Ok(result.Value.ToResponse()) : result.ToProblem(HttpContext);
    }
}