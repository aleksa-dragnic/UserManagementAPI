using Serilog.Context;

namespace UserManagementAPI.Api.Middleware;

/// <summary>
/// Every request carries a correlation id: the one the client sent, or a new
/// one. It is pushed onto the Serilog context, so every log line written while
/// the request runs carries it, and returned in the response — which is what
/// turns "it failed around three o'clock" into one grep.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[HeaderName].FirstOrDefault() is { Length: > 0 } fromClient
            ? fromClient
            : context.TraceIdentifier;

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;

            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }
}