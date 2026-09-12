using Asp.Versioning;

using Microsoft.AspNetCore.Mvc;

using UserManagementAPI.Api.Authorization;
using UserManagementAPI.Api.Contracts.V1;
using UserManagementAPI.Api.Extensions;
using UserManagementAPI.Api.Filters;
using UserManagementAPI.Api.Mapping;
using UserManagementAPI.Application.Abstractions;
using UserManagementAPI.Application.Users.Commands.LockUser;
using UserManagementAPI.Application.Users.Commands.RemoveRole;
using UserManagementAPI.Application.Users.Commands.UnlockUser;
using UserManagementAPI.Application.Users.Queries.GetUserById;
using UserManagementAPI.Domain.Roles;

namespace UserManagementAPI.Api.Controllers.V1;

/// <summary>
/// The users resource. Each write action maps the request to a command, each
/// read action builds a query; both dispatch and convert the Result to an
/// ActionResult. There is no other logic here — a rule that would need one
/// belongs in the aggregate, a read that would need one belongs in the query.
///
/// Every action names the permission it requires; there is no bare [Authorize]
/// anywhere in the project.
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/users")]
[Produces("application/json")]
public sealed class UsersController(IDispatcher dispatcher) : ControllerBase
{
    /// <summary>
    /// Lists users a page at a time, optionally filtered by status, searched by
    /// email or name, and sorted. Paging metadata is in the X-Pagination header.
    /// </summary>
    [HttpGet]
    [HasPermission(PermissionCodes.UsersRead)]
    [ETagFilter]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType<IReadOnlyList<UserResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetUsers(
        [FromQuery] UserQueryParameters parameters,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.QueryAsync(parameters.ToQuery(), cancellationToken);

        if (result.IsFailure)
        {
            return result.ToProblem(HttpContext);
        }

        PaginationHeader.Write(Response, result.Value.MetaData);

        return Ok(result.Value.Items.Select(user => user.ToResponse()).ToList());
    }

    /// <summary>Returns one user with the roles they hold.</summary>
    [HttpGet("{id:guid}")]
    [HasPermission(PermissionCodes.UsersRead)]
    [ETagFilter]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType<UserDetailsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUser(Guid id, CancellationToken cancellationToken)
    {
        var result = await dispatcher.QueryAsync(new GetUserByIdQuery(id), cancellationToken);

        return result.IsSuccess ? Ok(result.Value.ToResponse()) : result.ToProblem(HttpContext);
    }

    /// <summary>Registers a user. The account starts Pending until its email is verified.</summary>
    [HttpPost]
    [HasPermission(PermissionCodes.UsersWrite)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<UserCreatedResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Register(RegisterUserRequest request, CancellationToken cancellationToken)
    {
        var result = await dispatcher.SendAsync(request.ToCommand(), cancellationToken);

        if (result.IsFailure)
        {
            return result.ToProblem(HttpContext);
        }

        return Created($"/api/v1/users/{result.Value}", new UserCreatedResponse(result.Value));
    }

    /// <summary>Replaces the user's email and name.</summary>
    [HttpPut("{id:guid}")]
    [HasPermission(PermissionCodes.UsersWrite)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(Guid id, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var result = await dispatcher.SendAsync(request.ToCommand(id), cancellationToken);

        return result.IsSuccess ? NoContent() : result.ToProblem(HttpContext);
    }

    /// <summary>Locks the user. A locked user cannot log in (M4).</summary>
    [HttpPost("{id:guid}/lock")]
    [HasPermission(PermissionCodes.UsersLock)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Lock(Guid id, CancellationToken cancellationToken)
    {
        var result = await dispatcher.SendAsync(new LockUserCommand(id), cancellationToken);

        return result.IsSuccess ? NoContent() : result.ToProblem(HttpContext);
    }

    /// <summary>Removes the lock. Modelled as deleting the lock resource.</summary>
    [HttpDelete("{id:guid}/lock")]
    [HasPermission(PermissionCodes.UsersLock)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Unlock(Guid id, CancellationToken cancellationToken)
    {
        var result = await dispatcher.SendAsync(new UnlockUserCommand(id), cancellationToken);

        return result.IsSuccess ? NoContent() : result.ToProblem(HttpContext);
    }

    /// <summary>Assigns a role to the user.</summary>
    [HttpPost("{id:guid}/roles")]
    [HasPermission(PermissionCodes.RolesManage)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AssignRole(Guid id, AssignRoleRequest request, CancellationToken cancellationToken)
    {
        var result = await dispatcher.SendAsync(request.ToCommand(id), cancellationToken);

        return result.IsSuccess ? NoContent() : result.ToProblem(HttpContext);
    }

    /// <summary>Removes a role from the user. The last role cannot be removed.</summary>
    [HttpDelete("{id:guid}/roles/{roleId:guid}")]
    [HasPermission(PermissionCodes.RolesManage)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveRole(Guid id, Guid roleId, CancellationToken cancellationToken)
    {
        var result = await dispatcher.SendAsync(new RemoveRoleCommand(id, roleId), cancellationToken);

        return result.IsSuccess ? NoContent() : result.ToProblem(HttpContext);
    }
}