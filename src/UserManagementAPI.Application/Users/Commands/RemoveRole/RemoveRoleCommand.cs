using UserManagementAPI.Application.Abstractions;

namespace UserManagementAPI.Application.Users.Commands.RemoveRole;

public sealed record RemoveRoleCommand(Guid UserId, Guid RoleId) : ICommand;