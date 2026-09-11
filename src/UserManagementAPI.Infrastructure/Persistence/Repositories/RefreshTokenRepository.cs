using Microsoft.EntityFrameworkCore;

using UserManagementAPI.Domain.Auth;

namespace UserManagementAPI.Infrastructure.Persistence.Repositories;

/// <summary>
/// Write-side access to refresh tokens. Reads are by hash — the raw token is
/// never stored and never compared.
/// </summary>
public sealed class RefreshTokenRepository(
    AppDbContext context,
    DbContextOptions<AppDbContext> options) : IRefreshTokenRepository
{
    public Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        context.RefreshTokens.FirstOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

    public void Add(RefreshToken refreshToken) => context.RefreshTokens.Add(refreshToken);

    /// <summary>
    /// Runs on a second DbContext over its own connection, so the writes commit
    /// independently of the request transaction. The request that detected the
    /// reuse returns a failed Result, and the transaction behavior rolls it back
    /// — the revocation is the one write that must not go with it. The tokens
    /// are still revoked through the aggregate's own method.
    /// </summary>
    public async Task RevokeAllActiveForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var independent = new AppDbContext(options);

        var active = await independent.RefreshTokens
            .Where(token => token.UserId == userId && token.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var token in active)
        {
            token.Revoke();
        }

        await independent.SaveChangesAsync(cancellationToken);
    }
}