using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi;

namespace UserManagementAPI.Api.OpenApi;

/// <summary>
/// Two refusals no action declares, because the pipeline raises them before an
/// action runs: 429 on every rate-limited operation, and 413 on every operation
/// that takes a body, which Kestrel caps at 256 KB.
/// </summary>
internal sealed class LimitResponsesTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;

        if (metadata.OfType<EnableRateLimitingAttribute>().Any())
        {
            Add(operation, context, "429", "Too many requests in the policy's window. Retry-After says when to try again.");
        }

        if (context.Description.ParameterDescriptions.Any(parameter =>
                parameter.Source == Microsoft.AspNetCore.Mvc.ModelBinding.BindingSource.Body))
        {
            Add(operation, context, "413", "The request body is larger than the 256 KB the server accepts.");
        }

        return Task.CompletedTask;
    }

    private static void Add(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        string status,
        string description)
    {
        operation.Responses ??= new OpenApiResponses();

        if (operation.Responses.ContainsKey(status))
        {
            return;
        }

        operation.Responses[status] = new OpenApiResponse
        {
            Description = description,
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/problem+json"] = new()
                {
                    Schema = new OpenApiSchemaReference("ProblemDetails", context.Document)
                }
            }
        };
    }
}
