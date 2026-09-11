namespace UserManagementAPI.Application.Auth;

/// <summary>What a successful login returns. PR15 adds the refresh token.</summary>
public sealed record AuthTokens(string AccessToken, DateTime AccessTokenExpiresAtUtc);