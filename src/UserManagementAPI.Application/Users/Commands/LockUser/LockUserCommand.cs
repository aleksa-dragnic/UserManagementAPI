using UserManagementAPI.Application.Abstractions;

namespace UserManagementAPI.Application.Users.Commands.LockUser;

public sealed record LockUserCommand(Guid UserId, uint? ExpectedVersion = null) : ICommand;