using UserManagementAPI.Application.Abstractions;
using UserManagementAPI.Domain.Auth;
using UserManagementAPI.Domain.Common;
using UserManagementAPI.Domain.Users;

namespace UserManagementAPI.Application.Auth;

/// <summary>
/// The one place a token pair is produced, shared by login and refresh: look up
/// the permissions, create the access token, create a refresh token and store
/// its hash. When rotating, the new token is created through the old one so the
/// chain is recorded. The caller commits.
/// </summary>
public sealed class TokenIssuer(
    IPermissionLookup permissionLookup,
    ITokenService tokenService,
    IRefreshTokenRepository refreshTokenRepository)
{
    public async Task<Result<AuthTokens>> IssueAsync(
        User user,
        RefreshToken? rotating,
        CancellationToken cancellationToken = default)
    {
        var permissions = await permissionLookup.GetPermissionCodesAsync(user.Id, cancellationToken);

        var accessToken = tokenService.CreateAccessToken(user, permissions);
        var refreshToken = tokenService.CreateRefreshToken();
        var refreshTokenHash = tokenService.HashRefreshToken(refreshToken.Value);

        var stored = rotating is null
            ? RefreshToken.Issue(user.Id, refreshTokenHash, refreshToken.ExpiresAtUtc)
            : rotating.Rotate(refreshTokenHash, refreshToken.ExpiresAtUtc);

        if (stored.IsFailure)
        {
            return Result.Failure<AuthTokens>(stored.Error);
        }

        refreshTokenRepository.Add(stored.Value);

        return Result.Success(new AuthTokens(
            accessToken.Value,
            accessToken.ExpiresAtUtc,
            refreshToken.Value,
            refreshToken.ExpiresAtUtc));
    }
}