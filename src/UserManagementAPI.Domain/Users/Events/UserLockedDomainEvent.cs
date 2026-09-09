using UserManagementAPI.Domain.Common;

namespace UserManagementAPI.Domain.Users.Events;

public sealed record UserLockedDomainEvent(Guid UserId) : IDomainEvent
{
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}