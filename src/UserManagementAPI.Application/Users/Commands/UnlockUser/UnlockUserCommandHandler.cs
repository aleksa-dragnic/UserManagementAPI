using UserManagementAPI.Application.Abstractions;
using UserManagementAPI.Domain.Common;
using UserManagementAPI.Domain.Users;

namespace UserManagementAPI.Application.Users.Commands.UnlockUser;

public sealed class UnlockUserCommandHandler(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<UnlockUserCommand>
{
    public async Task<Result> HandleAsync(UnlockUserCommand command, CancellationToken cancellationToken = default)
    {
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;

        var user = await userRepository.GetByIdAsync(command.UserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure(User.NotFound);
        }

        if (!userRepository.IsCurrent(user, command.ExpectedVersion))
        {
            return Result.Failure(User.VersionMismatch);
        }

        var unlocked = user.Unlock(utcNow);

        if (unlocked.IsFailure)
        {
            return unlocked;
        }

        userRepository.Update(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}