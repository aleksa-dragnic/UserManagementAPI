namespace UserManagementAPI.Application.Auth;

/// <summary>What a successful login or refresh returns.</summary>
public sealed record AuthTokens(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc);