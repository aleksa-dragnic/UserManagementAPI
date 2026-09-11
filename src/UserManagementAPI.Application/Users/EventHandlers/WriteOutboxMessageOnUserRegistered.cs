using UserManagementAPI.Application.Abstractions;
using UserManagementAPI.Domain.Users.Events;

namespace UserManagementAPI.Application.Users.EventHandlers;

/// <summary>
/// A registration is something the outside world will care about — a welcome
/// email, a downstream service. None of that can run inside the transaction, so
/// the handler records the intent and the outbox processor does the rest.
/// </summary>
public sealed class WriteOutboxMessageOnUserRegistered(IOutboxWriter outbox)
    : IDomainEventHandler<UserRegisteredDomainEvent>
{
    public Task HandleAsync(UserRegisteredDomainEvent domainEvent, CancellationToken cancellationToken = default) =>
        outbox.WriteAsync(domainEvent, cancellationToken);
}