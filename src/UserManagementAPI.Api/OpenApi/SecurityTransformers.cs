using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace UserManagementAPI.Api.OpenApi;

/// <summary>
/// Declares the bearer scheme every protected operation uses, so a generated
/// client knows to send the access token and Scalar's auth box is wired without
/// filling it by hand.
/// </summary>
internal sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    public const string SchemeName = "Bearer";

    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[SchemeName] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "The access token the login and refresh endpoints return."
        };

        return Task.CompletedTask;
    }
}

/// <summary>
/// Marks every operation that requires a permission as needing the bearer
/// token. Anonymous operations — the root document, the auth endpoints and the
/// health checks — are left without a requirement.
/// </summary>
internal sealed class BearerSecurityRequirementTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;

        var requiresToken = metadata.OfType<IAuthorizeData>().Any() &&
                            !metadata.OfType<IAllowAnonymous>().Any();

        if (!requiresToken)
        {
            return Task.CompletedTask;
        }

        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(BearerSecuritySchemeTransformer.SchemeName, context.Document)] = []
        });

        return Task.CompletedTask;
    }
}
