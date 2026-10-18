using UserManagementAPI.Application.Abstractions;

namespace UserManagementAPI.Application.Users.Commands.UnlockUser;

public sealed record UnlockUserCommand(Guid UserId, uint? ExpectedVersion = null) : ICommand;