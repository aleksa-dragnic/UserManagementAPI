namespace UserManagementAPI.Infrastructure.Idempotency;

/// <summary>Bound from the "Idempotency" configuration section (ADR 0023).</summary>
public sealed class IdempotencyOptions
{
    public const string SectionName = "Idempotency";

    /// <summary>How long a key is remembered after it was claimed.</summary>
    public TimeSpan Retention { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// After this long, a claim whose response was never recorded is taken to
    /// belong to a request that died, and the key can be claimed again. Longer
    /// than any request this API serves.
    /// </summary>
    public TimeSpan Lease { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>Time between deletions of expired keys.</summary>
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromHours(1);
}
