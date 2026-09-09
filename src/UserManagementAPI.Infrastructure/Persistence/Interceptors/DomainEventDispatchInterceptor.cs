using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

using UserManagementAPI.Application.Abstractions;
using UserManagementAPI.Domain.Common;

namespace UserManagementAPI.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Collects domain events from every tracked aggregate and dispatches them
/// before the write is committed. Dispatching before commit is what puts the
/// handlers inside the same transaction: if a handler throws, the original
/// write rolls back with it (ADR 0007).
/// </summary>
public sealed class DomainEventDispatchInterceptor(IDomainEventPublisher publisher) : SaveChangesInterceptor
{
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            await DispatchAsync(eventData.Context, cancellationToken);
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private async Task DispatchAsync(DbContext context, CancellationToken cancellationToken)
    {
        var aggregates = context.ChangeTracker
            .Entries<AggregateRoot>()
            .Where(entry => entry.Entity.DomainEvents.Count > 0)
            .Select(entry => entry.Entity)
            .ToList();

        var domainEvents = aggregates
            .SelectMany(aggregate => aggregate.DomainEvents)
            .ToList();

        // Cleared before dispatch, so a handler that touches the same aggregate
        // and triggers a nested save does not replay the events it is handling.
        foreach (var aggregate in aggregates)
        {
            aggregate.ClearDomainEvents();
        }

        foreach (var domainEvent in domainEvents)
        {
            await publisher.PublishAsync(domainEvent, cancellationToken);
        }
    }
}