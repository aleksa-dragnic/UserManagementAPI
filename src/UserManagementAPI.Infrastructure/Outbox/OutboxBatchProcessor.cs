using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using UserManagementAPI.Infrastructure.Persistence;

namespace UserManagementAPI.Infrastructure.Outbox;

/// <summary>
/// One poll: take a batch of due, unprocessed messages, publish each, record the
/// outcome. Scoped, so it owns a fresh DbContext per poll. Separate from the
/// hosted service so a test can run one batch without a timer.
///
/// A failing message backs off exponentially and is abandoned at the configured
/// ceiling, so a poison message costs a few retries and then stops — it never
/// blocks the messages behind it.
/// </summary>
public sealed class OutboxBatchProcessor(
    AppDbContext context,
    IOutboxPublisher publisher,
    IOptions<OutboxOptions> options,
    ILogger<OutboxBatchProcessor> logger)
{
    private readonly OutboxOptions _options = options.Value;

    /// <summary>Returns the number of messages published in this batch.</summary>
    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var messages = await context.OutboxMessages
            .Where(message =>
                message.ProcessedOnUtc == null &&
                message.Attempts < _options.MaxAttempts &&
                (message.NextAttemptOnUtc == null || message.NextAttemptOnUtc <= now))
            .OrderBy(message => message.OccurredOnUtc)
            .Take(_options.BatchSize)
            .ToListAsync(cancellationToken);

        var published = 0;

        foreach (var message in messages)
        {
            try
            {
                await publisher.PublishAsync(message, cancellationToken);

                message.MarkProcessed(DateTime.UtcNow);
                published++;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Doubling from the base: 10 s, 20 s, 40 s, ... for the defaults.
                var backoff = _options.BaseBackoff * Math.Pow(2, message.Attempts);

                message.RecordFailure(exception.Message, DateTime.UtcNow, backoff);

                if (message.Attempts >= _options.MaxAttempts)
                {
                    logger.LogError(
                        exception,
                        "Outbox message {MessageId} of type {MessageType} abandoned after {Attempts} attempts.",
                        message.Id,
                        message.Type,
                        message.Attempts);
                }
                else
                {
                    logger.LogWarning(
                        exception,
                        "Outbox message {MessageId} of type {MessageType} failed on attempt {Attempts}; next attempt at {NextAttemptOnUtc:O}.",
                        message.Id,
                        message.Type,
                        message.Attempts,
                        message.NextAttemptOnUtc);
                }
            }
        }

        if (messages.Count > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        return published;
    }
}