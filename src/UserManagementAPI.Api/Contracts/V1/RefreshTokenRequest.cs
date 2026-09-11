namespace UserManagementAPI.Api.Contracts.V1;

/// <summary>Body of both refresh and logout: the refresh token being presented.</summary>
public sealed record RefreshTokenRequest(string RefreshToken);