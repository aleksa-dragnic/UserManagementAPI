using System.Security.Cryptography;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

using UserManagementAPI.Api.Extensions;
using UserManagementAPI.Application.Abstractions;
using UserManagementAPI.Domain.Common;

namespace UserManagementAPI.Api.Filters;

/// <summary>
/// Makes a POST safe to retry when the client sends an <c>Idempotency-Key</c>
/// (ADR 0023). Without the header the action runs as it always did. With it:
///
///   first request          runs, and a 2xx response is remembered
///   same key, same body    the remembered response again, with Idempotency-Replayed: true
///   same key, other body   422 Idempotency.KeyReused
///   same key, still running 409 Idempotency.InProgress
///   malformed key          400 Request.InvalidIdempotencyKey
///
/// A response that is not 2xx gives the key back, so a corrected retry runs.
///
/// A resource filter: it runs after authorization, so an anonymous caller is
/// refused before any key is claimed, and before model binding, so it can read
/// the request body and still leave it for the action.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class IdempotentAttribute : Attribute, IAsyncResourceFilter
{
    public const string HeaderName = "Idempotency-Key";

    public const string ReplayedHeaderName = "Idempotency-Replayed";

    private const int MaxKeyLength = 255;

    public static readonly Error InvalidKey = new(
        "Request.InvalidIdempotencyKey",
        $"{HeaderName} must be one value of 1 to {MaxKeyLength} visible ASCII characters.");

    public static readonly Error KeyReused = new(
        "Idempotency.KeyReused",
        $"This {HeaderName} was already used for a request with a different body.",
        ErrorType.Validation);

    public static readonly Error InProgress = new(
        "Idempotency.InProgress",
        $"A request with this {HeaderName} is still being processed.",
        ErrorType.Conflict);

    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var http = context.HttpContext;

        if (!http.Request.Headers.TryGetValue(HeaderName, out var values))
        {
            await next();
            return;
        }

        if (values.Count != 1 || !IsValidKey(values[0]))
        {
            context.Result = Result.Failure(InvalidKey).ToProblem(http);
            return;
        }

        var services = http.RequestServices;

        // Authorization ran first, so a caller is known; the scope is per caller.
        if (services.GetRequiredService<ICurrentUser>().UserId is not { } callerId)
        {
            await next();
            return;
        }

        var store = services.GetRequiredService<IIdempotencyStore>();
        var scope = new IdempotencyScope(callerId, Operation(context), values[0]!);
        var requestHash = await HashBodyAsync(http.Request);

        switch (await store.ClaimAsync(scope, requestHash, http.RequestAborted))
        {
            case IdempotencyClaim.InProgress:
                context.Result = Result.Failure(InProgress).ToProblem(http);
                return;

            case IdempotencyClaim.Mismatch:
                context.Result = Result.Failure(KeyReused).ToProblem(http);
                return;

            case IdempotencyClaim.Replay replay:
                context.Result = new ReplayResult(replay.Response);
                return;
        }

        await RunAndRememberAsync(context, next, store, scope);
    }

    private static async Task RunAndRememberAsync(
        ResourceExecutingContext context,
        ResourceExecutionDelegate next,
        IIdempotencyStore store,
        IdempotencyScope scope)
    {
        var response = context.HttpContext.Response;
        var original = response.Body;
        await using var buffer = new MemoryStream();
        response.Body = buffer;

        ResourceExecutedContext executed;

        try
        {
            executed = await next();
        }
        catch
        {
            response.Body = original;
            await store.ReleaseAsync(scope, CancellationToken.None);
            throw;
        }

        response.Body = original;

        // CancellationToken.None below, not the request's token: a client that
        // hung up must not leave its key claimed, and a response is remembered
        // even if nobody is listening any more.
        if (executed.Exception is not null && !executed.ExceptionHandled)
        {
            // The exception goes on to the exception handler, which writes to
            // the restored body.
            await store.ReleaseAsync(scope, CancellationToken.None);
            return;
        }

        var body = buffer.ToArray();

        if (response.StatusCode is >= 200 and < 300)
        {
            await store.CompleteAsync(
                scope,
                new StoredResponse(
                    response.StatusCode,
                    response.Headers.Location.ToString() is { Length: > 0 } location ? location : null,
                    response.ContentType,
                    body),
                CancellationToken.None);
        }
        else
        {
            await store.ReleaseAsync(scope, CancellationToken.None);
        }

        await original.WriteAsync(body);
    }

    /// <summary>
    /// Printable ASCII without spaces: the IETF draft's key is an opaque string,
    /// and refusing control characters keeps it safe to log and store.
    /// </summary>
    private static bool IsValidKey(string? key) =>
        key is { Length: > 0 and <= MaxKeyLength } && key.All(character => character is > ' ' and <= '~');

    /// <summary>The method and route template: the same key on two endpoints is two keys.</summary>
    private static string Operation(ResourceExecutingContext context) =>
        $"{context.HttpContext.Request.Method} {context.ActionDescriptor.AttributeRouteInfo?.Template}";

    private static async Task<string> HashBodyAsync(HttpRequest request)
    {
        request.EnableBuffering();
        var hash = await SHA256.HashDataAsync(request.Body, request.HttpContext.RequestAborted);
        request.Body.Position = 0;

        return Convert.ToHexString(hash);
    }

    /// <summary>The remembered response, byte for byte, marked as a replay.</summary>
    private sealed class ReplayResult(StoredResponse stored) : IActionResult
    {
        public async Task ExecuteResultAsync(ActionContext context)
        {
            var response = context.HttpContext.Response;

            response.StatusCode = stored.StatusCode;
            response.Headers[ReplayedHeaderName] = "true";

            if (stored.Location is not null)
            {
                response.Headers.Location = stored.Location;
            }

            if (stored.ContentType is not null)
            {
                response.ContentType = stored.ContentType;
            }

            await response.Body.WriteAsync(stored.Body);
        }
    }
}
