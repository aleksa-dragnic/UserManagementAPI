using UserManagementAPI.Domain.Users;

namespace UserManagementAPI.Application.Abstractions;

/// <summary>The claim types the API issues and the policies check.</summary>
public static class AuthClaimTypes
{
    /// <summary>One claim per permission code. Roles are not in the token; permissions are what policies check.</summary>
    public const string Permission = "permission";
}

/// <summary>A token string and the instant it stops being valid.</summary>
public sealed record IssuedToken(string Value, DateTime ExpiresAtUtc);

/// <summary>
/// Issues credentials. Implemented in Infrastructure — the signing algorithm,
/// key and lifetime are configuration, and the domain knows nothing of JWTs.
/// </summary>
public interface ITokenService
{
    IssuedToken CreateAccessToken(User user, IReadOnlyCollection<string> permissions);
}