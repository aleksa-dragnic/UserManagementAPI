using Asp.Versioning;
using Asp.Versioning.OpenApi;

using UserManagementAPI.Api.OpenApi;

namespace UserManagementAPI.Api.Extensions;

/// <summary>
/// URL-segment versioning (api/v1/..., api/v2/...) and one OpenAPI document per
/// version. The version is part of the address, so it is visible in logs, in a
/// browser and in every link the API generates.
/// </summary>
public static class ApiVersioningExtensions
{
    /// <summary>The OpenAPI documents, one per API version. Scalar lists the same names.</summary>
    public static readonly string[] Documents = ["v1", "v2"];

    public static IServiceCollection AddVersionedApi(this IServiceCollection services)
    {
        services
            .AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new ApiVersion(1, 0);
                options.ReportApiVersions = true;
                options.ApiVersionReader = new UrlSegmentApiVersionReader();
            })
            .AddMvc()
            .AddApiExplorer(options =>
            {
                // Group names "v1", "v2" — the built-in OpenAPI generator includes
                // an endpoint in a document when its group name matches the
                // document name, so each document sees only its own version.
                options.GroupNameFormat = "'v'VVV";
                options.SubstituteApiVersionInUrl = true;
            })
            // Asp.Versioning's own AddOpenApi, not the one on IServiceCollection:
            // it registers the versioned document services in place of the single
            // version-less document OpenAPI would describe by itself. Calling the
            // plain AddOpenApi() as well registers services that are then replaced
            // (analyzer AV0029). One document per version then comes from
            // WithDocumentPerVersion() on the mapped endpoint in Program.cs.
            //
            // The transformers describe what the pipeline does around the
            // actions: the bearer scheme, the problem details extensions, the
            // closed set of user states, and the 413 and 429 refusals.
            .AddOpenApi(options =>
            {
                options.Document.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
                options.Document.AddOperationTransformer<BearerSecurityRequirementTransformer>();
                options.Document.AddOperationTransformer<LimitResponsesTransformer>();
                options.Document.AddSchemaTransformer<ProblemDetailsSchemaTransformer>();
                options.Document.AddSchemaTransformer<UserStatusSchemaTransformer>();
            });

        return services;
    }
}