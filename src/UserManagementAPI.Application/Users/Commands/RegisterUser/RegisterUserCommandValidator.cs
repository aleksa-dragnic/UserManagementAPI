using FluentValidation;

using UserManagementAPI.Domain.Users;

namespace UserManagementAPI.Application.Users.Commands.RegisterUser;

/// <summary>
/// Shape only. Whether the email is taken or the user may be registered is the
/// domain's question, asked in the handler and the aggregate.
/// </summary>
public sealed class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    public const int MinPasswordLength = 8;

    public const int MaxPasswordLength = 128;

    public RegisterUserCommandValidator()
    {
        RuleFor(command => command.Email)
            .NotEmpty()
            .MaximumLength(Email.MaxLength)
            .EmailAddress();

        RuleFor(command => command.FirstName)
            .NotEmpty()
            .MaximumLength(PersonName.MaxPartLength);

        RuleFor(command => command.LastName)
            .NotEmpty()
            .MaximumLength(PersonName.MaxPartLength);

        RuleFor(command => command.Password)
            .NotEmpty()
            .MinimumLength(MinPasswordLength)
            .MaximumLength(MaxPasswordLength);
    }
}