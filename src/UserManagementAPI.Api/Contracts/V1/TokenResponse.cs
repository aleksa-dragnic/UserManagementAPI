namespace UserManagementAPI.Api.Contracts.V1;

/// <summary>
/// Body of a successful login or refresh: the access token and when it
/// expires. The refresh token travels only in the umapi_rt cookie (ADR 0019).
/// </summary>
public sealed record TokenResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc);