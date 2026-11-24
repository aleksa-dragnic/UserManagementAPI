using UserManagementAPI.Domain.Common;

namespace UserManagementAPI.Domain.Users.Events;

/// <summary>Raised by <c>User.Lock</c>, stamped with the time the aggregate was given.</summary>
public sealed record UserLockedDomainEvent(Guid UserId, DateTime OccurredOnUtc) : IDomainEvent;