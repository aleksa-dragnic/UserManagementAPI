using System.Text.Json;

using UserManagementAPI.Application.Common;

namespace UserManagementAPI.Api.Extensions;

/// <summary>
/// Paging metadata travels in a header, as camelCase JSON, so the body of a
/// collection stays a plain array. A browser client can only read it if CORS
/// exposes it — see the CORS policy.
/// </summary>
public static class PaginationHeader
{
    public const string Name = "X-Pagination";

    public static void Write(HttpResponse response, MetaData metaData) =>
        response.Headers[Name] = JsonSerializer.Serialize(metaData, JsonSerializerOptions.Web);
}