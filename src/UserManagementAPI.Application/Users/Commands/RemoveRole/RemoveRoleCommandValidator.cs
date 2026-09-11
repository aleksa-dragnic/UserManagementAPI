using FluentValidation;

namespace UserManagementAPI.Application.Users.Commands.RemoveRole;

public sealed class RemoveRoleCommandValidator : AbstractValidator<RemoveRoleCommand>
{
    public RemoveRoleCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();
        RuleFor(command => command.RoleId).NotEmpty();
    }
}