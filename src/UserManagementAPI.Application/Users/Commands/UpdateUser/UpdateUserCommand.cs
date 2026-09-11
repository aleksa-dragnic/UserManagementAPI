using UserManagementAPI.Application.Abstractions;

namespace UserManagementAPI.Application.Users.Commands.UpdateUser;

/// <summary>Replaces the user's email and name. Full replacement, matching PUT semantics.</summary>
public sealed record UpdateUserCommand(
    Guid UserId,
    string Email,
    string FirstName,
    string LastName) : ICommand;