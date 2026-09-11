using UserManagementAPI.Application.Abstractions;
using UserManagementAPI.Domain.Common;
using UserManagementAPI.Domain.Users;

namespace UserManagementAPI.Application.Users.Commands.RegisterUser;

/// <summary>
/// Orchestration only: build the value objects, refuse a taken email, ask the
/// aggregate to register, persist. Every rule about what a valid user is lives
/// in the value objects and in User.Register.
/// </summary>
public sealed class RegisterUserCommandHandler(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork) : ICommandHandler<RegisterUserCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(RegisterUserCommand command, CancellationToken cancellationToken = default)
    {
        var email = Email.Create(command.Email);

        if (email.IsFailure)
        {
            return Result.Failure<Guid>(email.Error);
        }

        if (await userRepository.ExistsByEmailAsync(email.Value, cancellationToken))
        {
            return Result.Failure<Guid>(User.EmailNotUnique);
        }

        var name = PersonName.Create(command.FirstName, command.LastName);

        if (name.IsFailure)
        {
            return Result.Failure<Guid>(name.Error);
        }

        var passwordHash = PasswordHash.Create(passwordHasher.Hash(command.Password));

        if (passwordHash.IsFailure)
        {
            return Result.Failure<Guid>(passwordHash.Error);
        }

        var user = User.Register(email.Value, name.Value, passwordHash.Value);

        if (user.IsFailure)
        {
            return Result.Failure<Guid>(user.Error);
        }

        userRepository.Add(user.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(user.Value.Id);
    }
}