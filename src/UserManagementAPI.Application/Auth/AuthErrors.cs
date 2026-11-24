using UserManagementAPI.Application.Common;
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

    public const string TooManyFailedAttemptsCode = "Auth.TooManyFailedAttempts";

    /// <summary>
    /// Too many wrong passwords for one email in a row (ADR 0024). The same
    /// answer for a known and an unknown email, and for a right and a wrong
    /// password while it lasts, so it reveals nothing about the account.
    /// </summary>
    public static RateLimitedError TooManyFailedAttempts(TimeSpan retryAfter) => new(
        TooManyFailedAttemptsCode,
        "Too many failed sign-in attempts for this email. Try again later.",
        retryAfter);

    public static readonly Error RefreshTokenReused = new(
        "Auth.RefreshTokenReused",
        "The refresh token was already exchanged. Every session for this account has been revoked.",
        ErrorType.Unauthorized);
}