using UserManagementAPI.Application.Abstractions;
using UserManagementAPI.Domain.Common;
using UserManagementAPI.Domain.Users;

namespace UserManagementAPI.Application.Users.Commands.RemoveRole;

public sealed class RemoveRoleCommandHandler(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork) : ICommandHandler<RemoveRoleCommand>
{
    public async Task<Result> HandleAsync(RemoveRoleCommand command, CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByIdAsync(command.UserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure(User.NotFound);
        }

        if (!userRepository.IsCurrent(user, command.ExpectedVersion))
        {
            return Result.Failure(User.VersionMismatch);
        }

        var removed = user.RemoveRole(command.RoleId);

        if (removed.IsFailure)
        {
            return removed;
        }

        userRepository.Update(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}