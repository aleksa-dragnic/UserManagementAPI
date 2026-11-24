using System.Globalization;

using Microsoft.AspNetCore.Mvc;

using UserManagementAPI.Api.Errors;
using UserManagementAPI.Application.Common;
using UserManagementAPI.Domain.Common;

namespace UserManagementAPI.Api.Extensions;

/// <summary>
/// Converts a failed Result into an IActionResult carrying problem details with
/// the status the error's type maps to. A controller action calls this
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

        // A refusal that ends by itself says when, as the rate limiter's does.
        if (result.Error is RateLimitedError limited)
        {
            httpContext.Response.Headers.RetryAfter =
                ((int)Math.Ceiling(limited.RetryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        }

        // A JsonResult, not an ObjectResult: [Produces] on a controller rewrites
        // the content types of every ObjectResult an action returns, and the
        // problem would go out as application/json. RFC 9457 says
        // application/problem+json, and a JsonResult keeps it.
        return new JsonResult(problem)
        {
            StatusCode = problem.Status,
            ContentType = "application/problem+json"
        };
    }
}