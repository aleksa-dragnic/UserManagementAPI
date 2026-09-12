using Asp.Versioning;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

using UserManagementAPI.Api.Authorization;
using UserManagementAPI.Api.Contracts.V1;
using UserManagementAPI.Api.Extensions;
using UserManagementAPI.Api.Filters;
using UserManagementAPI.Api.Hateoas;
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
/// anywhere in the project. The reads answer HEAD as well as GET, carry an
/// ETag, and add links when the client asks for application/vnd.umapi.hateoas+json.
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/users")]
[Produces("application/json")]
[EnableRateLimiting(RateLimitingExtensions.ReadPolicy)]
public sealed class UsersController(IDispatcher dispatcher, UserLinkGenerator links) : ControllerBase
{
    /// <summary>
    /// Lists users a page at a time, optionally filtered by status, searched by
    /// email or name, and sorted. Paging metadata is in the X-Pagination header.
    /// </summary>
    [HttpGet(Name = RouteNames.GetUsers)]
    [HttpHead]
    [HasPermission(PermissionCodes.UsersRead)]
    [ValidateMediaType]
    [ETagFilter]
    [Produces("application/json", HateoasMediaTypes.Hateoas)]
    [ProducesResponseType<IReadOnlyList<UserResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status406NotAcceptable)]
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

        var users = result.Value.Items.Select(user => user.ToResponse()).ToList();

        if (!HttpContext.WantsHateoas())
        {
            return Ok(users);
        }

        var linked = users
            .Select(user => new LinkedResource<UserResponse>(user, links.ForUser(HttpContext, user.Id)))
            .ToList();

        return Ok(new LinkedResource<IReadOnlyList<LinkedResource<UserResponse>>>(
            linked,
            links.ForCollection(HttpContext, parameters, result.Value.MetaData)));
    }

    /// <summary>Returns one user with the roles they hold.</summary>
    [HttpGet("{id:guid}", Name = RouteNames.GetUser)]
    [HttpHead("{id:guid}")]
    [HasPermission(PermissionCodes.UsersRead)]
    [ValidateMediaType]
    [ETagFilter]
    [Produces("application/json", HateoasMediaTypes.Hateoas)]
    [ProducesResponseType<UserDetailsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> GetUser(Guid id, CancellationToken cancellationToken)
    {
        var result = await dispatcher.QueryAsync(new GetUserByIdQuery(id), cancellationToken);

        if (result.IsFailure)
        {
            return result.ToProblem(HttpContext);
        }

        var user = result.Value.ToResponse();

        return HttpContext.WantsHateoas()
            ? Ok(new LinkedResource<UserDetailsResponse>(user, links.ForUser(HttpContext, id)))
            : Ok(user);
    }

    /// <summary>The methods the users collection supports, in the Allow header.</summary>
    [HttpOptions]
    [HasPermission(PermissionCodes.UsersRead)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Options()
    {
        Response.Headers.Allow = "GET, HEAD, POST, OPTIONS";

        return Ok();
    }

    /// <summary>Registers a user. The account starts Pending until its email is verified.</summary>
    [HttpPost(Name = RouteNames.RegisterUser)]
    [HasPermission(PermissionCodes.UsersWrite)]
    [EnableRateLimiting(RateLimitingExtensions.WritePolicy)]
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

        return CreatedAtRoute(
            RouteNames.GetUser,
            new { id = result.Value, version = "1" },
            new UserCreatedResponse(result.Value));
    }

    /// <summary>Replaces the user's email and name.</summary>
    [HttpPut("{id:guid}", Name = RouteNames.UpdateUser)]
    [HasPermission(PermissionCodes.UsersWrite)]
    [EnableRateLimiting(RateLimitingExtensions.WritePolicy)]
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
    [HttpPost("{id:guid}/lock", Name = RouteNames.LockUser)]
    [HasPermission(PermissionCodes.UsersLock)]
    [EnableRateLimiting(RateLimitingExtensions.WritePolicy)]
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
    [HttpDelete("{id:guid}/lock", Name = RouteNames.UnlockUser)]
    [HasPermission(PermissionCodes.UsersLock)]
    [EnableRateLimiting(RateLimitingExtensions.WritePolicy)]
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
    [HttpPost("{id:guid}/roles", Name = RouteNames.AssignRole)]
    [HasPermission(PermissionCodes.RolesManage)]
    [EnableRateLimiting(RateLimitingExtensions.WritePolicy)]
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
    [HttpDelete("{id:guid}/roles/{roleId:guid}", Name = RouteNames.RemoveRole)]
    [HasPermission(PermissionCodes.RolesManage)]
    [EnableRateLimiting(RateLimitingExtensions.WritePolicy)]
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