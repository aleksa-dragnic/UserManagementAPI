namespace UserManagementAPI.Api.Contracts.V1;

/// <summary>Body of a successful login or refresh.</summary>
public sealed record TokenResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc);