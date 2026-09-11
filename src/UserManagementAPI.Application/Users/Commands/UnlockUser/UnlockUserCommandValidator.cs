using FluentValidation;

namespace UserManagementAPI.Application.Users.Commands.UnlockUser;

public sealed class UnlockUserCommandValidator : AbstractValidator<UnlockUserCommand>
{
    public UnlockUserCommandValidator() => RuleFor(command => command.UserId).NotEmpty();
}