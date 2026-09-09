using UserManagementAPI.Domain.Common;

namespace UserManagementAPI.Domain.Users.Events;

/// <summary>
/// Carries the user id and nothing else. A handler that needs the user loads it;
/// a snapshot copied into the event would be stale by the time it ran.
/// </summary>
public sealed record UserRegisteredDomainEvent(Guid UserId) : IDomainEvent
{
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}