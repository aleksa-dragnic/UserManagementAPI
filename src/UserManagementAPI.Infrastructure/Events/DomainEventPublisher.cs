using Microsoft.Extensions.DependencyInjection;

using UserManagementAPI.Application.Abstractions;
using UserManagementAPI.Domain.Common;

namespace UserManagementAPI.Infrastructure.Events;

/// <summary>
/// Dispatches an event to every handler registered for its concrete type. The
/// closed generic is built at runtime because the interceptor holds events as
/// IDomainEvent and only the instance knows what it actually is.
/// </summary>
internal sealed class DomainEventPublisher(IServiceProvider serviceProvider) : IDomainEventPublisher
{
    public async Task PublishAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(domainEvent.GetType());

        var handleMethod = handlerType.GetMethod(
            nameof(IDomainEventHandler<IDomainEvent>.HandleAsync))!;

        foreach (var handler in serviceProvider.GetServices(handlerType))
        {
            if (handler is null)
            {
                continue;
            }

            await (Task)handleMethod.Invoke(handler, [domainEvent, cancellationToken])!;
        }
    }
}