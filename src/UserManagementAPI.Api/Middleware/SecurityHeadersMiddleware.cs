namespace UserManagementAPI.Api.Middleware;

/// <summary>
/// The headers a JSON API should always send. A REST API is not rendered in a
/// browser, but a browser can still be made to fetch one, so the cheap defences
/// go on every response: no MIME sniffing, no framing, no referrer leakage, and
/// a content security policy that permits nothing at all.
///
/// The API documentation is the exception: Scalar is a real page that loads its
/// own script and styles, and "default-src 'none'" would leave it blank. Those
/// two paths get a policy that allows what the viewer needs and nothing else.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    private const string ApiPolicy =
        "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";

    private const string DocumentationPolicy =
        "default-src 'self'; script-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net; " +
        "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com https://cdn.jsdelivr.net; " +
        "font-src 'self' https://fonts.gstatic.com data:; img-src 'self' data: https:; " +
        "connect-src 'self'; frame-ancestors 'none'; base-uri 'self'";

    public Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;

        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        headers["Content-Security-Policy"] = IsDocumentation(context.Request.Path)
            ? DocumentationPolicy
            : ApiPolicy;

        return next(context);
    }

    private static bool IsDocumentation(PathString path) =>
        path.StartsWithSegments("/scalar") || path.StartsWithSegments("/openapi");
}