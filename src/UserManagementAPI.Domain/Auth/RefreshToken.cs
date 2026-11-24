using UserManagementAPI.Domain.Common;

namespace UserManagementAPI.Domain.Auth;

/// <summary>
/// A refresh token, its own aggregate. It rotates on every exchange; inside User
/// it would load and lock the whole user on every token refresh.
///
/// Only the hash of the token is held. A database leak must not yield usable
/// tokens. ReplacedById records the rotation chain: a token that was already
/// exchanged and is presented again is proof of theft, and every active token
/// of that user is revoked rather than the one token presented.
/// </summary>
public sealed class RefreshToken : AggregateRoot
{
    public static readonly Error HashEmpty = new("RefreshToken.HashEmpty", "A token hash must be provided.");

    public static readonly Error NotActive = new(
        "RefreshToken.NotActive",
        "The refresh token is expired, revoked or has already been exchanged.");

    public static readonly Error AlreadyRevoked = new(
        "RefreshToken.AlreadyRevoked",
        "The refresh token is already revoked.",
        ErrorType.Conflict);

    private RefreshToken(Guid id, Guid userId, string tokenHash, DateTime expiresAtUtc, DateTime utcNow)
        : base(id)
    {
        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAtUtc = expiresAtUtc;
        CreatedAtUtc = utcNow;
    }

    /// <summary>Required by EF Core.</summary>
    private RefreshToken()
    {
    }

    public Guid UserId { get; private set; }

    public string TokenHash { get; private set; } = null!;

    public DateTime ExpiresAtUtc { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? RevokedAtUtc { get; private set; }

    /// <summary>The token this one was exchanged for; set by Rotate, never cleared.</summary>
    public Guid? ReplacedById { get; private set; }

    public bool IsRevoked => RevokedAtUtc is not null;

    public bool IsRotated => ReplacedById is not null;

    /// <summary>Expired from <see cref="ExpiresAtUtc"/> on, that instant included.</summary>
    public bool IsExpiredAt(DateTime utcNow) => utcNow >= ExpiresAtUtc;

    public bool IsActiveAt(DateTime utcNow) => !IsExpiredAt(utcNow) && !IsRevoked && !IsRotated;

    public static Result<RefreshToken> Issue(Guid userId, string tokenHash, DateTime expiresAtUtc, DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            return Result.Failure<RefreshToken>(HashEmpty);
        }

        return Result.Success(new RefreshToken(Guid.NewGuid(), userId, tokenHash, expiresAtUtc, utcNow));
    }

    /// <summary>
    /// Exchanges this token for a new one. This token becomes inactive by virtue
    /// of being replaced; it is not revoked, because "replaced" is the state
    /// that reuse detection looks for.
    /// </summary>
    public Result<RefreshToken> Rotate(string newTokenHash, DateTime newExpiresAtUtc, DateTime utcNow)
    {
        if (!IsActiveAt(utcNow))
        {
            return Result.Failure<RefreshToken>(NotActive);
        }

        var replacement = Issue(UserId, newTokenHash, newExpiresAtUtc, utcNow);

        if (replacement.IsFailure)
        {
            return replacement;
        }

        ReplacedById = replacement.Value.Id;

        return replacement;
    }

    public Result Revoke(DateTime utcNow)
    {
        if (IsRevoked)
        {
            return Result.Failure(AlreadyRevoked);
        }

        RevokedAtUtc = utcNow;

        return Result.Success();
    }
}