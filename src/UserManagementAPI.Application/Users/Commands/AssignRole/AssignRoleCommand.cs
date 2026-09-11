using UserManagementAPI.Application.Abstractions;

namespace UserManagementAPI.Application.Users.Commands.AssignRole;

public sealed record AssignRoleCommand(Guid UserId, Guid RoleId) : ICommand;