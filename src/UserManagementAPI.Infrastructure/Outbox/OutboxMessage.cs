namespace UserManagementAPI.Infrastructure.Outbox;

/// <summary>
/// One row in outbox_messages: an event serialized at the moment it happened,
/// waiting to be published. Infrastructure, not domain — nothing in the domain
/// knows this table exists.
/// </summary>
public sealed class OutboxMessage
{
    public const int MaxErrorLength = 2000;

    public OutboxMessage(Guid id, string type, string payload, DateTime occurredOnUtc)
    {
        Id = id;
        Type = type;
        Payload = payload;
        OccurredOnUtc = occurredOnUtc;
    }

    /// <summary>Required by EF Core.</summary>
    private OutboxMessage()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>Full CLR name of the event type; what a publisher switches on.</summary>
    public string Type { get; private set; } = null!;

    /// <summary>The event as JSON, stored as jsonb.</summary>
    public string Payload { get; private set; } = null!;

    public DateTime OccurredOnUtc { get; private set; }

    public DateTime? ProcessedOnUtc { get; private set; }

    public int Attempts { get; private set; }

    public string? Error { get; private set; }

    /// <summary>Earliest time the next attempt may run; null until the first failure.</summary>
    public DateTime? NextAttemptOnUtc { get; private set; }

    public void MarkProcessed(DateTime utcNow)
    {
        ProcessedOnUtc = utcNow;
        Error = null;
    }

    public void RecordFailure(string error, DateTime utcNow, TimeSpan backoff)
    {
        Attempts++;
        Error = error.Length <= MaxErrorLength ? error : error[..MaxErrorLength];
        NextAttemptOnUtc = utcNow + backoff;
    }
}