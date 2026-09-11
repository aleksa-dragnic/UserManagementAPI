using FluentValidation;

namespace UserManagementAPI.Application.Auth.Commands.Logout;

public sealed class LogoutCommandValidator : AbstractValidator<LogoutCommand>
{
    public LogoutCommandValidator() => RuleFor(command => command.RefreshToken).NotEmpty();
}