using FluentValidation;

namespace UserManagementAPI.Application.Users.Commands.AssignRole;

public sealed class AssignRoleCommandValidator : AbstractValidator<AssignRoleCommand>
{
    public AssignRoleCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();
        RuleFor(command => command.RoleId).NotEmpty();
    }
}