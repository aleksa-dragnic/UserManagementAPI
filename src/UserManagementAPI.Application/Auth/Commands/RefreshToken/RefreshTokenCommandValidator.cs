using FluentValidation;

namespace UserManagementAPI.Application.Auth.Commands.RefreshToken;

public sealed class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator() => RuleFor(command => command.RefreshToken).NotEmpty();
}