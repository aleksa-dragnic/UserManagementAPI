using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

using UserManagementAPI.Api.Hateoas;

namespace UserManagementAPI.Api.Filters;

/// <summary>
/// Content negotiation for the read endpoints that offer links. An Accept
/// header that names nothing this endpoint can produce — an unknown vendor type,
/// text/xml — is answered with 406 rather than with JSON the client did not ask
/// for. When the client names the HATEOAS type, the action is told so through
/// HttpContext.WantsHateoas(). No Accept header at all means "anything".
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class ValidateMediaTypeAttribute : ActionFilterAttribute
{
    private static readonly string[] Producible =
    [
        "application/json",
        HateoasMediaTypes.Hateoas,
        "application/*",
        "*/*"
    ];

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var accept = context.HttpContext.Request.GetTypedHeaders().Accept;

        if (accept.Count == 0)
        {
            return;
        }

        if (!accept.Any(media => Producible.Contains(media.MediaType.Value ?? string.Empty, StringComparer.OrdinalIgnoreCase)))
        {
            context.Result = new StatusCodeResult(StatusCodes.Status406NotAcceptable);
            return;
        }

        if (accept.Any(media => string.Equals(media.MediaType.Value, HateoasMediaTypes.Hateoas, StringComparison.OrdinalIgnoreCase)))
        {
            context.HttpContext.Items[HateoasMediaTypes.RequestedItemKey] = true;
        }
    }
}