using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace UserManagementAPI.Infrastructure.Identity;

/// <summary>
/// Deletes forgotten login-failure rows every cleanup interval (ADR 0024). A
/// forgotten run already counts from one again, so this only bounds the table,
/// which unknown emails would otherwise grow; a missed run changes no answer.
/// </summary>
internal sealed class LoginFailureCleanup(
    IServiceScopeFactory scopeFactory,
    IOptions<LoginThrottleOptions> options,
    ILogger<LoginFailureCleanup> logger) : BackgroundService
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
                    var deleted = await scope.ServiceProvider.GetRequiredService<LoginThrottle>()
                        .DeleteForgottenAsync(stoppingToken);

                    if (deleted > 0)
                    {
                        logger.LogInformation("Deleted {Count} forgotten login-failure rows.", deleted);
                    }
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    logger.LogError(exception, "Login-failure cleanup failed; retrying next interval.");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down.
        }
    }
}
