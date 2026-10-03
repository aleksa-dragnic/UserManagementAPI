using UserManagementAPI.Application.Abstractions;
using UserManagementAPI.Domain.Common;
using UserManagementAPI.Domain.Users;

namespace UserManagementAPI.Application.Users.Commands.LockUser;

/// <summary>
/// Locks a user. Nobody locks their own account: there is no last-administrator
/// rule, and an administrator who locked themselves could leave no one able to
/// unlock anyone. The check needs the caller, which the domain does not know,
/// so it runs here, before the user is loaded.
/// </summary>
public sealed class LockUserCommandHandler(
    IUserRepository userRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork) : ICommandHandler<LockUserCommand>
{
    public async Task<Result> HandleAsync(LockUserCommand command, CancellationToken cancellationToken = default)
    {
        if (currentUser.UserId == command.UserId)
        {
            return Result.Failure(User.CannotLockSelf);
        }

        var user = await userRepository.GetByIdAsync(command.UserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure(User.NotFound);
        }

        var locked = user.Lock();

        if (locked.IsFailure)
        {
            return locked;
        }

        userRepository.Update(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}