using System.Diagnostics;

using Microsoft.AspNetCore.Mvc;

using UserManagementAPI.Application.Common;
using UserManagementAPI.Domain.Common;

namespace UserManagementAPI.Api.Errors;

/// <summary>
/// Turns a domain or application Error into RFC 9457 problem details. The
/// status follows the error's type, never its code:
///
///   Validation                       422 Unprocessable Content
///   Unauthorized                     401 Unauthorized
///   NotFound                         404 Not Found
///   Conflict                         409 Conflict
///   PreconditionFailed               412 Precondition Failed
///   Failure                          400 Bad Request
///
/// The code itself travels in the "errorCode" extension so a client can branch
/// on something stable while the human-readable title and detail may change.
/// </summary>
internal static class ErrorMapping
{
    public static int StatusCodeFor(Error error) => error.Type switch
    {
        ErrorType.Validation => StatusCodes.Status422UnprocessableEntity,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.PreconditionFailed => StatusCodes.Status412PreconditionFailed,
        _ => StatusCodes.Status400BadRequest
    };

    public static ProblemDetails ToProblemDetails(Error error, HttpContext httpContext)
    {
        var status = StatusCodeFor(error);

        var problem = new ProblemDetails
        {
            Type = ProblemTypes.For(status),
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

    private static string TitleFor(int status) => status switch
    {
        StatusCodes.Status401Unauthorized => "Unauthorized",
        StatusCodes.Status404NotFound => "Not Found",
        StatusCodes.Status409Conflict => "Conflict",
        StatusCodes.Status412PreconditionFailed => "Precondition Failed",
        StatusCodes.Status422UnprocessableEntity => "Unprocessable Content",
        _ => "Bad Request"
    };
}