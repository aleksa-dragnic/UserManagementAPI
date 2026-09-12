using UserManagementAPI.Api.Contracts.V1;
using UserManagementAPI.Application.Common;

namespace UserManagementAPI.Api.Hateoas;

/// <summary>
/// The links for the users resource. The same set is offered whatever the
/// user's state: which transitions are legal is the aggregate's decision, made
/// when the request arrives, and duplicating that rule here would give it a
/// second place to go stale.
/// </summary>
public sealed class UserLinkGenerator(LinkFactory links)
{
    public IReadOnlyList<Link> ForUser(HttpContext httpContext, Guid id) =>
    [
        links.CreateV1(httpContext, RouteNames.GetUser, "self", HttpMethods.Get, Id(id)),
        links.CreateV1(httpContext, RouteNames.UpdateUser, "update", HttpMethods.Put, Id(id)),
        links.CreateV1(httpContext, RouteNames.LockUser, "lock", HttpMethods.Post, Id(id)),
        links.CreateV1(httpContext, RouteNames.UnlockUser, "unlock", HttpMethods.Delete, Id(id)),
        links.CreateV1(httpContext, RouteNames.AssignRole, "assign-role", HttpMethods.Post, Id(id))
    ];

    public IReadOnlyList<Link> ForCollection(
        HttpContext httpContext,
        UserQueryParameters parameters,
        MetaData metaData)
    {
        var result = new List<Link>
        {
            links.CreateV1(httpContext, RouteNames.GetUsers, "self", HttpMethods.Get, Page(parameters, metaData, metaData.CurrentPage))
        };

        if (metaData.HasPrevious)
        {
            result.Add(links.CreateV1(httpContext, RouteNames.GetUsers, "previous", HttpMethods.Get, Page(parameters, metaData, metaData.CurrentPage - 1)));
        }

        if (metaData.HasNext)
        {
            result.Add(links.CreateV1(httpContext, RouteNames.GetUsers, "next", HttpMethods.Get, Page(parameters, metaData, metaData.CurrentPage + 1)));
        }

        result.Add(links.CreateV1(httpContext, RouteNames.RegisterUser, "create", HttpMethods.Post));

        return result;
    }

    private static RouteValueDictionary Id(Guid id) => new() { ["id"] = id };

    /// <summary>
    /// The query string of another page: the same filter, search and sort, the
    /// effective (clamped) page size, and the new page number. Values that were
    /// not supplied are left out rather than sent empty.
    /// </summary>
    private static RouteValueDictionary Page(UserQueryParameters parameters, MetaData metaData, int pageNumber)
    {
        var values = new RouteValueDictionary
        {
            ["pageNumber"] = pageNumber,
            ["pageSize"] = metaData.PageSize
        };

        if (!string.IsNullOrWhiteSpace(parameters.SearchTerm))
        {
            values["searchTerm"] = parameters.SearchTerm;
        }

        if (!string.IsNullOrWhiteSpace(parameters.Status))
        {
            values["status"] = parameters.Status;
        }

        if (!string.IsNullOrWhiteSpace(parameters.OrderBy))
        {
            values["orderBy"] = parameters.OrderBy;
        }

        return values;
    }
}