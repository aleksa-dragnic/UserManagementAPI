using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace UserManagementAPI.Api.Errors;

/// <summary>
/// The response to a request the framework rejected before an action ran: a body
/// it could not parse, a route value of the wrong type. Everything else in this
/// API answers a failure with application/problem+json (RFC 9457), and the
/// default InvalidModelStateResponseFactory does not: it offers application/json
/// first, so a client that states no preference is told "json" and has to guess
/// that the object it received is a problem document. Only the content type
/// changes here; the status and the body are the ones the framework produced.
/// </summary>
internal static class ProblemDetailsResponses
{
    public static IActionResult FromModelState(ActionContext context)
    {
        var factory = context.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>();
        var problem = factory.CreateValidationProblemDetails(context.HttpContext, context.ModelState);

        problem.Instance ??= context.HttpContext.Request.Path;

        // A JsonResult for the same reason ResultExtensions uses one: [Produces]
        // on a controller rewrites the content types of every ObjectResult, and
        // an ObjectResult here would go out as application/json whatever it asked
        // for. A JsonResult keeps its ContentType.
        return new JsonResult(problem)
        {
            StatusCode = problem.Status ?? StatusCodes.Status400BadRequest,
            ContentType = "application/problem+json"
        };
    }
}