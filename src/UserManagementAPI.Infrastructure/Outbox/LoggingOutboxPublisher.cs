using Microsoft.Extensions.Logging;

namespace UserManagementAPI.Infrastructure.Outbox;

/// <summary>
/// The transport is a log line. There is no message broker in this project and
/// pretending otherwise would be dishonest; the pattern — write in the
/// transaction, publish afterwards with retry — is what is being demonstrated,
/// and a real publisher replaces this one registration. The payload is not
/// logged: an event may carry data that does not belong in a log.
/// </summary>
internal sealed class LoggingOutboxPublisher(ILogger<LoggingOutboxPublisher> logger) : IOutboxPublisher
{
    public Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Published outbox message {MessageId} of type {MessageType} that occurred at {OccurredOnUtc:O}.",
            message.Id,
            message.Type,
            message.OccurredOnUtc);

        return Task.CompletedTask;
    }
}