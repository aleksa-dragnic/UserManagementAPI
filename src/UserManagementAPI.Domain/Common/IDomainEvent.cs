namespace UserManagementAPI.Domain.Common;

/// <summary>
/// Marker for something that happened inside the domain. Implementations are
/// records carrying identifiers only — never an entity reference, which would
/// be stale by the time the handler runs.
/// </summary>
public interface IDomainEvent
{
    DateTime OccurredOnUtc { get; }
}