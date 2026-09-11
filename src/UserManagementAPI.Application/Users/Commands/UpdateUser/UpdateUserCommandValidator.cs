using FluentValidation;

using UserManagementAPI.Domain.Users;

namespace UserManagementAPI.Application.Users.Commands.UpdateUser;

public sealed class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();

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
    }
}