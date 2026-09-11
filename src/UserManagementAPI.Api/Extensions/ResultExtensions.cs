using Microsoft.AspNetCore.Mvc;

using UserManagementAPI.Api.Errors;
using UserManagementAPI.Domain.Common;

namespace UserManagementAPI.Api.Extensions;

/// <summary>
/// Converts a failed Result into an IActionResult carrying problem details with
/// the status the error's code family maps to. A controller action calls this
/// on the failure path and nothing else — it never inspects an error code.
/// </summary>
public static class ResultExtensions
{
    public static IActionResult ToProblem(this Result result, HttpContext httpContext)
    {
        if (result.IsSuccess)
        {
            throw new InvalidOperationException("A successful result has no problem to report.");
        }

        var problem = ErrorMapping.ToProblemDetails(result.Error, httpContext);

        return new ObjectResult(problem)
        {
            StatusCode = problem.Status,
            ContentTypes = { "application/problem+json" }
        };
    }
}