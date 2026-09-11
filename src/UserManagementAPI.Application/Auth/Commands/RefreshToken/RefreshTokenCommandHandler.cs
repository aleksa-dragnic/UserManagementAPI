using UserManagementAPI.Application.Abstractions;
using UserManagementAPI.Domain.Auth;
using UserManagementAPI.Domain.Common;
using UserManagementAPI.Domain.Users;

namespace UserManagementAPI.Application.Auth.Commands.RefreshToken;

/// <summary>
/// Exchanges a refresh token for a new pair, rotating the old one. A token that
/// was already exchanged and is presented again means the chain is compromised:
/// every active token for that user is revoked — on its own connection, so the
/// revocation survives this request's rollback — and the request is refused.
/// </summary>
public sealed class RefreshTokenCommandHandler(
    IRefreshTokenRepository refreshTokenRepository,
    IUserRepository userRepository,
    ITokenService tokenService,
    TokenIssuer tokenIssuer,
    IUnitOfWork unitOfWork) : ICommandHandler<RefreshTokenCommand, AuthTokens>
{
    public async Task<Result<AuthTokens>> HandleAsync(RefreshTokenCommand command, CancellationToken cancellationToken = default)
    {
        var tokenHash = tokenService.HashRefreshToken(command.RefreshToken);
        var presented = await refreshTokenRepository.GetByTokenHashAsync(tokenHash, cancellationToken);

        if (presented is null)
        {
            return Result.Failure<AuthTokens>(AuthErrors.InvalidRefreshToken);
        }

        if (presented.IsRotated)
        {
            await refreshTokenRepository.RevokeAllActiveForUserAsync(presented.UserId, cancellationToken);

            return Result.Failure<AuthTokens>(AuthErrors.RefreshTokenReused);
        }

        if (!presented.IsActive)
        {
            return Result.Failure<AuthTokens>(AuthErrors.InvalidRefreshToken);
        }

        var user = await userRepository.GetByIdAsync(presented.UserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure<AuthTokens>(AuthErrors.InvalidRefreshToken);
        }

        var canLogIn = user.EnsureCanLogIn();

        if (canLogIn.IsFailure)
        {
            return Result.Failure<AuthTokens>(canLogIn.Error);
        }

        var tokens = await tokenIssuer.IssueAsync(user, presented, cancellationToken);

        if (tokens.IsFailure)
        {
            return tokens;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return tokens;
    }
}