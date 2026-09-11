using UserManagementAPI.Application.Abstractions;

namespace UserManagementAPI.Application.Auth.Commands.Logout;

public sealed record LogoutCommand(string RefreshToken) : ICommand;