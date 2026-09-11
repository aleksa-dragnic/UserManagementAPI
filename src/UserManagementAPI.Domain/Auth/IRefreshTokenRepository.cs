namespace UserManagementAPI.Domain.Auth;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    void Add(RefreshToken refreshToken);

    /// <summary>
    /// Revokes every active token the user holds, and does so on its own
    /// connection, outside the caller's transaction. It exists for reuse
    /// detection: the request that revealed the theft is refused and rolled back,
    /// and the revocation must survive that rollback.
    /// </summary>
    Task RevokeAllActiveForUserAsync(Guid userId, CancellationToken cancellationToken = default);
}