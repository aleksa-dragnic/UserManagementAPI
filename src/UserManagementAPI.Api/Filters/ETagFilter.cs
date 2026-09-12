using System.Text;
using System.Text.Json;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

using UserManagementAPI.Api.Caching;
using UserManagementAPI.Api.Hateoas;

namespace UserManagementAPI.Api.Filters;

/// <summary>
/// Conditional GET by validation: the server computes an entity tag from the
/// representation it is about to send, and answers 304 with no body when the
/// client already holds that exact representation (If-None-Match). User data
/// changes unpredictably, so there is no expiration window — every reuse is
/// revalidated ("private, no-cache").
///
/// The tag covers the serialized body and, when present, the X-Pagination
/// header: two pages can have identical bodies (both empty) and different
/// metadata, and a 304 must not hide the difference. The representation also
/// depends on Accept (plain JSON or HATEOAS), hence Vary: Accept.
///
/// HEAD gets the same headers as GET — the same ETag included — and no body.
/// The body is dropped here rather than left to the server, because not every
/// host strips it (the test server does not).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class ETagFilter : ResultFilterAttribute
{
    public override async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        var httpContext = context.HttpContext;
        var method = httpContext.Request.Method;

        if ((HttpMethods.IsGet(method) || HttpMethods.IsHead(method)) &&
            context.Result is ObjectResult { Value: { } value } result &&
            result.StatusCode is null or StatusCodes.Status200OK)
        {
            var etag = ComputeETag(httpContext, value);
            var headers = httpContext.Response.Headers;

            headers.ETag = etag;
            headers.CacheControl = "private, no-cache";
            headers.Append(HeaderNames.Vary, HeaderNames.Accept);

            if (IfNoneMatchMatches(httpContext.Request, etag))
            {
                context.Result = new StatusCodeResult(StatusCodes.Status304NotModified);
            }
            else if (HttpMethods.IsHead(method))
            {
                httpContext.Response.ContentType = httpContext.WantsHateoas()
                    ? HateoasMediaTypes.Hateoas
                    : "application/json; charset=utf-8";

                context.Result = new EmptyResult();
            }
        }

        await next();
    }

    private static string ComputeETag(HttpContext httpContext, object value)
    {
        var serializerOptions = httpContext.RequestServices
            .GetRequiredService<IOptions<JsonOptions>>().Value.JsonSerializerOptions;

        var body = JsonSerializer.SerializeToUtf8Bytes(value, value.GetType(), serializerOptions);

        var pagination = httpContext.Response.Headers["X-Pagination"].ToString();

        if (pagination.Length == 0)
        {
            return ETagGenerator.Generate(body);
        }

        return ETagGenerator.Generate([.. body, .. Encoding.UTF8.GetBytes(pagination)]);
    }

    /// <summary>
    /// Weak comparison, as RFC 9110 requires for If-None-Match; "*" matches any
    /// current representation.
    /// </summary>
    private static bool IfNoneMatchMatches(HttpRequest request, string etag)
    {
        var candidates = request.GetTypedHeaders().IfNoneMatch;

        if (candidates.Count == 0)
        {
            return false;
        }

        var current = new EntityTagHeaderValue(etag);

        return candidates.Any(candidate =>
            candidate.Equals(EntityTagHeaderValue.Any) ||
            candidate.Compare(current, useStrongComparison: false));
    }
}