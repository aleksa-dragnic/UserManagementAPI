namespace UserManagementAPI.Infrastructure.Auditing;

/// <summary>
/// One row in audit_log: who did what to which entity, and which fields moved.
/// Append only — there is no method that changes an entry once it exists, and
/// no code path deletes one. The payload holds field names, never values: a
/// value can be a password hash or a token, a name cannot.
/// </summary>
public sealed class AuditLogEntry
{
    public AuditLogEntry(Guid? actorId, string action, string entity, string entityId, string payload, DateTime occurredAtUtc)
    {
        ActorId = actorId;
        Action = action;
        Entity = entity;
        EntityId = entityId;
        Payload = payload;
        OccurredAtUtc = occurredAtUtc;
    }

    /// <summary>Required by EF Core.</summary>
    private AuditLogEntry()
    {
    }

    public long Id { get; private set; }

    /// <summary>Null for a system write — the seeder, a background worker.</summary>
    public Guid? ActorId { get; private set; }

    /// <summary>Created, Updated or Deleted.</summary>
    public string Action { get; private set; } = null!;

    /// <summary>CLR name of the aggregate or entity, e.g. "User".</summary>
    public string Entity { get; private set; } = null!;

    /// <summary>Key values joined with ':' for composite keys.</summary>
    public string EntityId { get; private set; } = null!;

    /// <summary>jsonb: {"changedFields":[...]} — names only.</summary>
    public string Payload { get; private set; } = null!;

    public DateTime OccurredAtUtc { get; private set; }
}