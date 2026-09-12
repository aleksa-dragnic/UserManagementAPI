using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

using UserManagementAPI.Application.Abstractions;

namespace UserManagementAPI.Api.Errors;

/// <summary>
/// A lost optimistic-concurrency race is a conflict, not a server fault: the
/// request was fine when it was written and stale by the time it arrived.
/// Registered ahead of GlobalExceptionHandler so it does not become a 500.
/// </summary>
internal sealed class ConcurrencyExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not ConcurrencyConflictException)
        {
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status409Conflict;

        var problem = new ProblemDetails
        {
            Type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.10",
            Title = "Conflict",
            Status = StatusCodes.Status409Conflict,
            Detail = "The record was modified by another request. Read it again and retry.",
            Instance = $"{httpContext.Request.Method} {httpContext.Request.Path}"
        };

        problem.Extensions["errorCode"] = "Concurrency.Conflict";

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problem
        });
    }
}