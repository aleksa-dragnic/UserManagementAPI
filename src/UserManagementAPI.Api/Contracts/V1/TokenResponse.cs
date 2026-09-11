namespace UserManagementAPI.Api.Contracts.V1;

/// <summary>Body of a successful login. PR15 adds the refresh token.</summary>
public sealed record TokenResponse(string AccessToken, DateTime AccessTokenExpiresAtUtc);