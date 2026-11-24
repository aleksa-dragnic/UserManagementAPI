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
///
/// Safe with more than one replica: the batch is claimed with
/// <c>FOR UPDATE SKIP LOCKED</c> inside a transaction that lasts until the
/// outcome is saved, so a second processor polling at the same moment skips
/// the rows this one holds and takes the next ones, or none (ADR 0008).
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
        // With EnableRetryOnFailure on, a transaction has to run inside the
        // execution strategy (ADR 0002). A retry re-runs the whole batch, which
        // can publish a message twice; delivery is at least once either way.
        var strategy = context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(ProcessInTransactionAsync, cancellationToken);
    }

    private async Task<int> ProcessInTransactionAsync(CancellationToken cancellationToken)
    {
        // A retried attempt starts from what the database holds, not from
        // entities the failed attempt had already changed.
        context.ChangeTracker.Clear();

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        var now = DateTime.UtcNow;

        // The row locks are held until the transaction ends, so the claim covers
        // publishing as well as reading. SKIP LOCKED makes a concurrent poll
        // pass over the claimed rows instead of waiting for them.
        var messages = await context.OutboxMessages
            .FromSql($"""
                SELECT *
                FROM outbox_messages
                WHERE processed_on_utc IS NULL
                  AND attempts < {_options.MaxAttempts}
                  AND (next_attempt_on_utc IS NULL OR next_attempt_on_utc <= {now})
                ORDER BY occurred_on_utc
                LIMIT {_options.BatchSize}
                FOR UPDATE SKIP LOCKED
                """)
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

        await transaction.CommitAsync(cancellationToken);

        return published;
    }
}