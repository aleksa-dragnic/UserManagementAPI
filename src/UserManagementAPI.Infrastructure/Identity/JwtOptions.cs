namespace UserManagementAPI.Infrastructure.Identity;

/// <summary>
/// Bound from the "Jwt" section. SigningKey is a secret and lives in user
/// secrets locally and in an environment variable everywhere else; Issuer and
/// Audience are not secrets and have defaults in appsettings.json.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>HS256 needs 256 bits; 64 characters gives comfortable headroom over that.</summary>
    public const int MinimumSigningKeyLength = 64;

    public string SigningKey { get; set; } = string.Empty;

    public string Issuer { get; set; } = "usermanagementapi";

    public string Audience { get; set; } = "usermanagementapi";

    /// <summary>Short on purpose: a stolen access token is useful for this long and no longer.</summary>
    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>How long a session can stay idle before the user must log in again.</summary>
    public TimeSpan RefreshTokenLifetime { get; set; } = TimeSpan.FromDays(7);
}