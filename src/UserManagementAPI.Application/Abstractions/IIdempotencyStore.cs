namespace UserManagementAPI.Application.Abstractions;

/// <summary>
/// Remembers the outcome of a request a client marked with an idempotency key,
/// so a retry of it gets the same answer instead of doing the work twice
/// (ADR 0023). Defined here, implemented in Infrastructure, used by the Api.
/// </summary>
public interface IIdempotencyStore
{
    /// <summary>
    /// Claims the key for this request, or says why it cannot be claimed. Safe
    /// against a concurrent request with the same key: the database decides.
    /// </summary>
    Task<IdempotencyClaim> ClaimAsync(IdempotencyScope scope, string requestHash, CancellationToken cancellationToken = default);

    /// <summary>Stores the response of a claimed request so a retry replays it.</summary>
    Task CompleteAsync(IdempotencyScope scope, StoredResponse response, CancellationToken cancellationToken = default);

    /// <summary>Gives the key back after a request that did not succeed, so a corrected retry can run.</summary>
    Task ReleaseAsync(IdempotencyScope scope, CancellationToken cancellationToken = default);
}

/// <summary>A key belongs to one caller and one operation; two callers' keys never collide.</summary>
public sealed record IdempotencyScope(Guid CallerId, string Operation, string Key);

/// <summary>What a replay sends back: the original status, Location, content type and body.</summary>
public sealed record StoredResponse(int StatusCode, string? Location, string? ContentType, byte[] Body);

public abstract record IdempotencyClaim
{
    private IdempotencyClaim()
    {
    }

    /// <summary>The key is this request's: run it.</summary>
    public sealed record Claimed : IdempotencyClaim;

    /// <summary>A request with this key is still running.</summary>
    public sealed record InProgress : IdempotencyClaim;

    /// <summary>The key was used for a request with a different body.</summary>
    public sealed record Mismatch : IdempotencyClaim;

    /// <summary>The key's request completed; send its response again.</summary>
    public sealed record Replay(StoredResponse Response) : IdempotencyClaim;
}
