using UserManagementAPI.Application.Abstractions;

namespace UserManagementAPI.Application.Auth.Commands.Login;

public sealed record LoginCommand(string Email, string Password) : ICommand<AuthTokens>;