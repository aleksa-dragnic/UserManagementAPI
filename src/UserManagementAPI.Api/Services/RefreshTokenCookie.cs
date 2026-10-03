using System.Diagnostics.CodeAnalysis;

using UserManagementAPI.Application.Auth;

namespace UserManagementAPI.Api.Services;

/// <summary>
/// The refresh token's transport (ADR 0019): HttpOnly, Secure, host-only,
/// SameSite=Strict, scoped to the auth endpoints, and living exactly as long as
/// the token it carries. Written by login and refresh, read by refresh and
/// logout, cleared by logout and by a refused refresh.
/// </summary>
internal static class RefreshTokenCookie
{
    public const string CookieName = "umapi_rt";

    /// <summary>
    /// Every auth endpoint, not only refresh: logout revokes the token it
    /// receives, and a cookie scoped to /refresh would never reach it.
    /// </summary>
    public const string CookiePath = "/api/v1/auth";

    public static void Write(HttpResponse response, AuthTokens tokens)
    {
        var remaining = tokens.RefreshTokenExpiresAtUtc - DateTime.UtcNow;

        response.Cookies.Append(CookieName, tokens.RefreshToken, Options(remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero));
    }

    public static bool TryRead(HttpRequest request, [NotNullWhen(true)] out string? refreshToken)
    {
        if (request.Cookies.TryGetValue(CookieName, out var value) && !string.IsNullOrWhiteSpace(value))
        {
            refreshToken = value;
            return true;
        }

        refreshToken = null;
        return false;
    }

    /// <summary>
    /// A clear has to carry the name, path and attributes of the cookie it
    /// replaces; one that differs in Path leaves the original in place.
    /// </summary>
    public static void Clear(HttpResponse response) => response.Cookies.Delete(CookieName, Options(maxAge: null));

    // No Domain attribute: host-only, so no other subdomain ever receives it.
    private static CookieOptions Options(TimeSpan? maxAge) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = CookiePath,
        MaxAge = maxAge
    };
}