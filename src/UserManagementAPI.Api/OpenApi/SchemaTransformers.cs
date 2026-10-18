using System.Text.Json.Nodes;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

using UserManagementAPI.Domain.Common;
using UserManagementAPI.Domain.Users;

namespace UserManagementAPI.Api.OpenApi;

/// <summary>
/// The problem details the API writes carry three extensions the framework's
/// ProblemDetails type does not declare: <c>errorCode</c>, the stable code a
/// client branches on; <c>traceId</c>, the id a log line carries; and
/// <c>errors</c>, the field messages of a 422. Declared here so a generated
/// client has them as typed properties.
/// </summary>
internal sealed class ProblemDetailsSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (context.JsonTypeInfo.Type != typeof(ProblemDetails) || context.JsonPropertyInfo is not null)
        {
            return Task.CompletedTask;
        }

        schema.Properties ??= new Dictionary<string, IOpenApiSchema>();

        schema.Properties["errorCode"] = new OpenApiSchema
        {
            Type = JsonSchemaType.String | JsonSchemaType.Null,
            Description = "Stable code for a failure the application decided, such as User.EmailNotUnique. Absent on failures the framework writes."
        };

        schema.Properties["traceId"] = new OpenApiSchema
        {
            Type = JsonSchemaType.String | JsonSchemaType.Null,
            Description = "The W3C traceparent id of the request; its trace id part is the one in the logs."
        };

        schema.Properties["errors"] = new OpenApiSchema
        {
            Type = JsonSchemaType.Object | JsonSchemaType.Null,
            Description = "On a 422, and on a 400 from model binding: the messages for each field that failed.",
            AdditionalProperties = new OpenApiSchema
            {
                Type = JsonSchemaType.Array,
                Items = new OpenApiSchema { Type = JsonSchemaType.String }
            }
        };

        return Task.CompletedTask;
    }
}

/// <summary>
/// A user's status travels as a string so a new state does not break a client
/// that only displays it, but the values are a closed set: the UserStatus
/// enumeration. Listing them lets a generated client type the field.
/// </summary>
internal sealed class UserStatusSchemaTransformer : IOpenApiSchemaTransformer
{
    private static readonly string[] StatusNames =
        Enumeration.GetAll<UserStatus>().Select(status => status.Name).ToArray();

    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        var property = context.JsonPropertyInfo;

        if (property is null ||
            property.Name != "status" ||
            property.PropertyType != typeof(string) ||
            property.DeclaringType.Namespace?.StartsWith("UserManagementAPI.Api.Contracts", StringComparison.Ordinal) != true ||
            !property.DeclaringType.Name.StartsWith("User", StringComparison.Ordinal))
        {
            return Task.CompletedTask;
        }

        schema.Enum = StatusNames.Select(name => (JsonNode)JsonValue.Create(name)!).ToList();

        return Task.CompletedTask;
    }
}
