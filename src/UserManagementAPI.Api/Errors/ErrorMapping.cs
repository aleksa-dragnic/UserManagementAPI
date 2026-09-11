using System.Diagnostics;

using Microsoft.AspNetCore.Mvc;

using UserManagementAPI.Application.Common;
using UserManagementAPI.Domain.Common;

namespace UserManagementAPI.Api.Errors;

/// <summary>
/// Turns a domain or application Error into RFC 9457 problem details. The
/// status is chosen by error-code family, not by listing every code:
///
///   Validation.*                     422 Unprocessable Content
///   *.NotFound                       404 Not Found
///   *.NotUnique, *.Already*          409 Conflict
///   everything else                  400 Bad Request
///
/// The code itself travels in the "errorCode" extension so a client can branch
/// on something stable while the human-readable title and detail may change.
/// </summary>
internal static class ErrorMapping
{
    public static int StatusCodeFor(Error error)
    {
        if (error is ValidationError)
        {
            return StatusCodes.Status422UnprocessableEntity;
        }

        if (error.Code.EndsWith(".NotFound", StringComparison.Ordinal))
        {
            return StatusCodes.Status404NotFound;
        }

        if (error.Code.EndsWith(".NotUnique", StringComparison.Ordinal) ||
            error.Code.Contains(".Already", StringComparison.Ordinal))
        {
            return StatusCodes.Status409Conflict;
        }

        return StatusCodes.Status400BadRequest;
    }

    public static ProblemDetails ToProblemDetails(Error error, HttpContext httpContext)
    {
        var status = StatusCodeFor(error);

        var problem = new ProblemDetails
        {
            Type = TypeFor(status),
            Title = TitleFor(status),
            Status = status,
            Detail = error.Description,
            Instance = $"{httpContext.Request.Method} {httpContext.Request.Path}"
        };

        problem.Extensions["errorCode"] = error.Code;
        problem.Extensions["traceId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier;

        if (error is ValidationError validation)
        {
            // Same shape as ValidationProblemDetails: property name -> messages.
            problem.Extensions["errors"] = validation.Errors
                .GroupBy(entry => entry.Code[ValidationError.CodePrefix.Length..])
                .ToDictionary(group => group.Key, group => group.Select(entry => entry.Description).ToArray());
        }

        return problem;
    }

    private static string TypeFor(int status) => status switch
    {
        StatusCodes.Status404NotFound => "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.5",
        StatusCodes.Status409Conflict => "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.10",
        StatusCodes.Status422UnprocessableEntity => "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.21",
        _ => "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.1"
    };

    private static string TitleFor(int status) => status switch
    {
        StatusCodes.Status404NotFound => "Not Found",
        StatusCodes.Status409Conflict => "Conflict",
        StatusCodes.Status422UnprocessableEntity => "Unprocessable Content",
        _ => "Bad Request"
    };
}