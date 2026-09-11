using Microsoft.AspNetCore.Mvc;

using UserManagementAPI.Api.Contracts.V1;
using UserManagementAPI.Api.Extensions;
using UserManagementAPI.Api.Mapping;
using UserManagementAPI.Application.Abstractions;
using UserManagementAPI.Application.Users.Commands.LockUser;
using UserManagementAPI.Application.Users.Commands.RemoveRole;
using UserManagementAPI.Application.Users.Commands.UnlockUser;

namespace UserManagementAPI.Api.Controllers.V1;

/// <summary>
/// Write side of the users resource. Each action maps the request to a command,
/// dispatches it and converts the Result to an ActionResult. There is no other
/// logic here — a rule that would need one belongs in the aggregate.
///
/// The read side (GET) arrives in M5 PR18, and the permission gates in M4 PR16.
/// </summary>
[ApiController]
[Route("api/v1/users")]
[Produces("application/json")]
public sealed class UsersController(IDispatcher dispatcher) : ControllerBase
{
    /// <summary>Registers a user. The account starts Pending until its email is verified.</summary>
    [HttpPost]
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
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveRole(Guid id, Guid roleId, CancellationToken cancellationToken)
    {
        var result = await dispatcher.SendAsync(new RemoveRoleCommand(id, roleId), cancellationToken);

        return result.IsSuccess ? NoContent() : result.ToProblem(HttpContext);
    }
}