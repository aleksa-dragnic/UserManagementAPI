using UserManagementAPI.Application.Abstractions;
using UserManagementAPI.Domain.Common;
using UserManagementAPI.Domain.Roles;
using UserManagementAPI.Domain.Users;

namespace UserManagementAPI.Application.Users.Commands.AssignRole;

/// <summary>
/// The role is checked for existence, not loaded: User holds a RoleId, never a
/// Role, and the handler keeps to that boundary.
/// </summary>
public sealed class AssignRoleCommandHandler(
    IUserRepository userRepository,
    IRoleRepository roleRepository,
    IUnitOfWork unitOfWork) : ICommandHandler<AssignRoleCommand>
{
    public async Task<Result> HandleAsync(AssignRoleCommand command, CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByIdAsync(command.UserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure(User.NotFound);
        }

        if (!await roleRepository.ExistsAsync(command.RoleId, cancellationToken))
        {
            return Result.Failure(Role.NotFound);
        }

        var assigned = user.AssignRole(command.RoleId);

        if (assigned.IsFailure)
        {
            return assigned;
        }

        userRepository.Update(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}