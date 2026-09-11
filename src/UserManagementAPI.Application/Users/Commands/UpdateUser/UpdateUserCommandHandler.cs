using UserManagementAPI.Application.Abstractions;
using UserManagementAPI.Domain.Common;
using UserManagementAPI.Domain.Users;

namespace UserManagementAPI.Application.Users.Commands.UpdateUser;

public sealed class UpdateUserCommandHandler(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork) : ICommandHandler<UpdateUserCommand>
{
    public async Task<Result> HandleAsync(UpdateUserCommand command, CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByIdAsync(command.UserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure(User.NotFound);
        }

        var email = Email.Create(command.Email);

        if (email.IsFailure)
        {
            return Result.Failure(email.Error);
        }

        var name = PersonName.Create(command.FirstName, command.LastName);

        if (name.IsFailure)
        {
            return Result.Failure(name.Error);
        }

        // Only a change of address can collide; keeping the same one is not a conflict.
        if (user.Email != email.Value && await userRepository.ExistsByEmailAsync(email.Value, cancellationToken))
        {
            return Result.Failure(User.EmailNotUnique);
        }

        var emailChanged = user.ChangeEmail(email.Value);

        if (emailChanged.IsFailure)
        {
            return emailChanged;
        }

        var nameChanged = user.ChangeName(name.Value);

        if (nameChanged.IsFailure)
        {
            return nameChanged;
        }

        userRepository.Update(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}