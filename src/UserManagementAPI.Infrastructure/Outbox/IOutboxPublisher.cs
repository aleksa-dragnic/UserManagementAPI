namespace UserManagementAPI.Infrastructure.Outbox;

/// <summary>
/// Delivers one outbox message to wherever it is going. Throwing means the
/// delivery failed and the processor will retry with backoff; returning means it
/// was delivered and the message is marked processed.
/// </summary>
public interface IOutboxPublisher
{
    Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken = default);
}