using UserManagementAPI.Application.Abstractions;
using UserManagementAPI.Domain.Common;
using UserManagementAPI.Domain.Users;

namespace UserManagementAPI.Application.Users.Commands.UnlockUser;

public sealed class UnlockUserCommandHandler(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork) : ICommandHandler<UnlockUserCommand>
{
    public async Task<Result> HandleAsync(UnlockUserCommand command, CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByIdAsync(command.UserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure(User.NotFound);
        }

        var unlocked = user.Unlock();

        if (unlocked.IsFailure)
        {
            return unlocked;
        }

        userRepository.Update(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}