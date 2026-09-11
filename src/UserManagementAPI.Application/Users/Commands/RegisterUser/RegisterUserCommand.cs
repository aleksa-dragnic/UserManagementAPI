using UserManagementAPI.Application.Abstractions;

namespace UserManagementAPI.Application.Users.Commands.RegisterUser;

/// <summary>Returns the new user's id.</summary>
public sealed record RegisterUserCommand(
    string Email,
    string FirstName,
    string LastName,
    string Password) : ICommand<Guid>;