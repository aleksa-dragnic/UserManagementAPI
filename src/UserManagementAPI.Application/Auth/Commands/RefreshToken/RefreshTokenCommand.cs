using UserManagementAPI.Application.Abstractions;

namespace UserManagementAPI.Application.Auth.Commands.RefreshToken;

public sealed record RefreshTokenCommand(string RefreshToken) : ICommand<AuthTokens>;