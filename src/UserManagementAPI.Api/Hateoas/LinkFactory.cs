namespace UserManagementAPI.Api.Hateoas;

/// <summary>
/// Builds a Link from a route name through LinkGenerator, so an href is always
/// an address the routing table will actually match — never a string
/// concatenated by hand. A route that cannot be resolved is a programming
/// error and throws.
/// </summary>
public sealed class LinkFactory(LinkGenerator linkGenerator)
{
    private const string Version1 = "1";

    /// <summary>A link to a v1 route: the version segment is filled in.</summary>
    public Link CreateV1(
        HttpContext httpContext,
        string routeName,
        string rel,
        string method,
        RouteValueDictionary? values = null)
    {
        var routeValues = values ?? new RouteValueDictionary();
        routeValues["version"] = Version1;

        return Create(httpContext, routeName, rel, method, routeValues);
    }

    /// <summary>A link to a version-neutral route.</summary>
    public Link Create(
        HttpContext httpContext,
        string routeName,
        string rel,
        string method,
        RouteValueDictionary? values = null)
    {
        var href = linkGenerator.GetPathByRouteValues(httpContext, routeName, values ?? new RouteValueDictionary())
            ?? throw new InvalidOperationException($"No route named '{routeName}' matches the values given.");

        return new Link(href, rel, method);
    }
}