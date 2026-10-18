using System.Buffers;

using Serilog.Context;

namespace UserManagementAPI.Api.Middleware;

/// <summary>
/// Every request carries a correlation id: the one the client sent, or a new
/// one. It is pushed onto the Serilog context, so every log line written while
/// the request runs carries it, and returned in the response — which is what
/// turns "it failed around three o'clock" into one grep.
///
/// A client's id is taken only when it is short and made of letters, digits,
/// '-', '_' and '.'; anything else is replaced by a new one. The id is written
/// into every log line and echoed in a header, so an unchecked one would let a
/// caller put arbitrary text, or a lot of it, into both.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";

    public const int MaxLength = 64;

    private static readonly SearchValues<char> Allowed = SearchValues.Create(
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_.");

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[HeaderName].FirstOrDefault() is { } fromClient &&
                            IsWellFormed(fromClient)
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

    public static bool IsWellFormed(string candidate) =>
        candidate.Length is > 0 and <= MaxLength && !candidate.AsSpan().ContainsAnyExcept(Allowed);
}
