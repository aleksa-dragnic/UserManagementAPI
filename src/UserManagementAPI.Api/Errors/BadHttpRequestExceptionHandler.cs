using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace UserManagementAPI.Api.Errors;

/// <summary>
/// A request Kestrel refused to read is a client fault carrying its own status,
/// not a server fault. A body over the size limit is rejected with 413 while
/// the model binder is reading it, and the resulting exception travels the
/// pipeline like any other; without this handler GlobalExceptionHandler turns
/// it into 500 and logs a stack trace. 500 also tells the caller to retry
/// something that can never succeed.
///
/// Registered ahead of GlobalExceptionHandler, like the concurrency handler.
/// </summary>
internal sealed class BadHttpRequestExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<BadHttpRequestExceptionHandler> logger) : IExceptionHandler
{
    private const int ContentTooLarge = 413;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // Kestrel's own exception type derives from this one, so catching the
        // base covers both the server and the framework.
        if (exception is not BadHttpRequestException badRequest)
        {
            return false;
        }

        // Kestrel already decided the status: 413 for an oversized body, 400
        // for a request line or header it could not parse.
        var status = badRequest.StatusCode;

        // A warning, not an error. The caller sent something the server was
        // configured to refuse; nothing here needs an operator's attention, and
        // an unauthenticated caller should not be able to write error-level
        // entries at will.
        logger.LogWarning(
            "Rejected a {Method} request with {Status}: {Reason}",
            httpContext.Request.Method,
            status,
            badRequest.Message);

        httpContext.Response.StatusCode = status;

        var problem = new ProblemDetails
        {
            Type = status == ContentTooLarge
                ? "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.14"
                : "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.1",
            Title = status == ContentTooLarge ? "Content too large" : "Bad request",
            Status = status,
            Detail = status == ContentTooLarge
                ? "The request body exceeds the size this endpoint accepts."
                : "The request could not be read.",
            Instance = $"{httpContext.Request.Method} {httpContext.Request.Path}"
        };

        problem.Extensions["errorCode"] = status == ContentTooLarge
            ? "Request.ContentTooLarge"
            : "Request.Malformed";

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problem
        });
    }
}