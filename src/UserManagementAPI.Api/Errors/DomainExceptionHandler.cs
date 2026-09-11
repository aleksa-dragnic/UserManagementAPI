using Microsoft.AspNetCore.Diagnostics;

using UserManagementAPI.Domain.Common;

namespace UserManagementAPI.Api.Errors;

/// <summary>
/// Maps a DomainException — an invariant violated on a path that could not
/// return a Result — to the same problem details a failed Result produces, so
/// the client sees one shape whichever way the domain said no. Registered ahead
/// of GlobalExceptionHandler; anything that is not a DomainException passes
/// through to it.
/// </summary>
internal sealed class DomainExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not DomainException domainException)
        {
            return false;
        }

        var problem = ErrorMapping.ToProblemDetails(domainException.Error, httpContext);

        httpContext.Response.StatusCode = problem.Status!.Value;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problem
        });
    }
}