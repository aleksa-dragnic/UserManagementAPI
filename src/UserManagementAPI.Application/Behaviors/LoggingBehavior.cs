using System.Diagnostics;

using Microsoft.Extensions.Logging;

using UserManagementAPI.Application.Abstractions;
using UserManagementAPI.Domain.Common;

namespace UserManagementAPI.Application.Behaviors;

/// <summary>
/// Logs the request type on entry and the outcome with elapsed time on exit.
/// The request payload is never logged — commands carry passwords.
/// </summary>
public sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TResponse : Result
{
    public async Task<TResponse> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;

        logger.LogInformation("Handling {RequestName}", requestName);

        var started = Stopwatch.GetTimestamp();
        var response = await next();
        var elapsed = Stopwatch.GetElapsedTime(started);

        if (response.IsSuccess)
        {
            logger.LogInformation(
                "Handled {RequestName} in {ElapsedMilliseconds:0.0} ms",
                requestName,
                elapsed.TotalMilliseconds);
        }
        else
        {
            logger.LogWarning(
                "Failed {RequestName} with {ErrorCode} in {ElapsedMilliseconds:0.0} ms",
                requestName,
                response.Error.Code,
                elapsed.TotalMilliseconds);
        }

        return response;
    }
}