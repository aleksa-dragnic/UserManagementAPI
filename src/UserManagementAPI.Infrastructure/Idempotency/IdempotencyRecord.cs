namespace UserManagementAPI.Infrastructure.Idempotency;

/// <summary>
/// One row in idempotency_keys. Written only by <see cref="IdempotencyStore"/>
/// with SQL of its own, so claiming is atomic in the database; the entity exists
/// so the table is part of the model and its migration, and so it can be read.
/// Infrastructure, not domain: it is a property of the HTTP surface.
/// </summary>
public sealed class IdempotencyRecord
{
    public const string InProgress = "in_progress";

    public const string Completed = "completed";

    public const int MaxKeyLength = 255;

    /// <summary>Required by EF Core.</summary>
    private IdempotencyRecord()
    {
    }

    public Guid CallerId { get; private set; }

    public string Operation { get; private set; } = null!;

    public string Key { get; private set; } = null!;

    /// <summary>SHA-256 of the request body, hex; the same key with another body is refused.</summary>
    public string RequestHash { get; private set; } = null!;

    public string Status { get; private set; } = null!;

    public int? ResponseStatus { get; private set; }

    public string? ResponseLocation { get; private set; }

    public string? ResponseContentType { get; private set; }

    public byte[]? ResponseBody { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? CompletedAtUtc { get; private set; }

    public DateTime ExpiresAtUtc { get; private set; }
}
