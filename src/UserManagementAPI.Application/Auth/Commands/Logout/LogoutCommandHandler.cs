using UserManagementAPI.Application.Abstractions;
using UserManagementAPI.Domain.Auth;
using UserManagementAPI.Domain.Common;

namespace UserManagementAPI.Application.Auth.Commands.Logout;

/// <summary>
/// Revokes the presented refresh token. Idempotent: an unknown or already
/// revoked token is a logout that has already happened, not an error — a client
/// retrying after a dropped connection must not get a 401 for succeeding twice.
/// </summary>
public sealed class LogoutCommandHandler(
    IRefreshTokenRepository refreshTokenRepository,
    ITokenService tokenService,
    IUnitOfWork unitOfWork) : ICommandHandler<LogoutCommand>
{
    public async Task<Result> HandleAsync(LogoutCommand command, CancellationToken cancellationToken = default)
    {
        var tokenHash = tokenService.HashRefreshToken(command.RefreshToken);
        var presented = await refreshTokenRepository.GetByTokenHashAsync(tokenHash, cancellationToken);

        if (presented is null || presented.IsRevoked)
        {
            return Result.Success();
        }

        presented.Revoke();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}