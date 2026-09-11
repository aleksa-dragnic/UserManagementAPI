using FluentValidation;

namespace UserManagementAPI.Application.Auth.Commands.Login;

/// <summary>
/// Presence only. Deliberately no format rule on the email and no length rule
/// on the password: a 422 that says "not a valid email" for an address that is
/// simply not registered is the same enumeration hole as a distinct 401.
/// </summary>
public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(command => command.Email).NotEmpty();
        RuleFor(command => command.Password).NotEmpty();
    }
}