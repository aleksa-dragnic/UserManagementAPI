using UserManagementAPI.Domain.Common;

namespace UserManagementAPI.Application.Auth;

/// <summary>
/// Authentication failures, all of type Unauthorized (401). There is one error
/// for a wrong email and a wrong password because a distinguishable response
/// tells an attacker which addresses are registered.
/// </summary>
public static class AuthErrors
{
    public static readonly Error InvalidCredentials = new(
        "Auth.InvalidCredentials",
        "The email or password is incorrect.",
        ErrorType.Unauthorized);

    public static readonly Error InvalidRefreshToken = new(
        "Auth.InvalidRefreshToken",
        "The refresh token is not valid.",
        ErrorType.Unauthorized);

    public static readonly Error RefreshTokenReused = new(
        "Auth.RefreshTokenReused",
        "The refresh token was already exchanged. Every session for this account has been revoked.",
        ErrorType.Unauthorized);
}