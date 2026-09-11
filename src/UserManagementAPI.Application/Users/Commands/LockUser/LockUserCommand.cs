using UserManagementAPI.Application.Abstractions;

namespace UserManagementAPI.Application.Users.Commands.LockUser;

public sealed record LockUserCommand(Guid UserId) : ICommand;