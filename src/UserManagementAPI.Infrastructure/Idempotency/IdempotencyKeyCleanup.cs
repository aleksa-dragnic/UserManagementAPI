using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace UserManagementAPI.Infrastructure.Idempotency;

/// <summary>
/// Deletes expired idempotency keys every cleanup interval (ADR 0023). An
/// expired key is already treated as unclaimed, so this only bounds the table;
/// a missed run changes no answer. A failed run is logged and the loop goes on,
/// as the outbox processor's does.
/// </summary>
internal sealed class IdempotencyKeyCleanup(
    IServiceScopeFactory scopeFactory,
    IOptions<IdempotencyOptions> options,
    ILogger<IdempotencyKeyCleanup> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.CleanupInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var deleted = await scope.ServiceProvider.GetRequiredService<IdempotencyStore>()
                        .DeleteExpiredAsync(stoppingToken);

                    if (deleted > 0)
                    {
                        logger.LogInformation("Deleted {Count} expired idempotency keys.", deleted);
                    }
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    logger.LogError(exception, "Idempotency key cleanup failed; retrying next interval.");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down.
        }
    }
}
