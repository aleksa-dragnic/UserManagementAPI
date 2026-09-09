using UserManagementAPI.Domain.Common;

namespace UserManagementAPI.Application.Abstractions;

/// <summary>
/// Reacts to something that happened inside the domain. Handlers run before the
/// commit, inside the same transaction (ADR 0007), so a handler that throws
/// takes the original write down with it. Anything that must reach the outside
/// world goes through the outbox in M3 instead.
/// </summary>
public interface IDomainEventHandler<in TDomainEvent>
    where TDomainEvent : IDomainEvent
{
    Task HandleAsync(TDomainEvent domainEvent, CancellationToken cancellationToken = default);
}