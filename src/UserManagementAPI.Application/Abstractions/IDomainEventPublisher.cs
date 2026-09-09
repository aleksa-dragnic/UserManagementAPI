using UserManagementAPI.Domain.Common;

namespace UserManagementAPI.Application.Abstractions;

/// <summary>
/// Resolves and runs the handlers registered for a domain event. Implemented in
/// Infrastructure, called by the save-changes interceptor.
/// </summary>
public interface IDomainEventPublisher
{
    Task PublishAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default);
}