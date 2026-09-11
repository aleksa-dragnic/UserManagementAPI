using FluentValidation;

namespace UserManagementAPI.Application.Users.Commands.LockUser;

public sealed class LockUserCommandValidator : AbstractValidator<LockUserCommand>
{
    public LockUserCommandValidator() => RuleFor(command => command.UserId).NotEmpty();
}