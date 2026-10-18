using System.Text.Json;

namespace UserManagementAPI.IntegrationTests.Functional;

/// <summary>
/// The OpenAPI documents describe what the pipeline does around the actions:
/// the bearer scheme and which operations need it, the problem details the API
/// writes, the closed set of user states, and the 413 and 429 refusals. Clients
/// generate their types from these documents, so each of these is asserted.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class OpenApiDocumentTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private ApiFactory _factory = null!;

    public async Task InitializeAsync()
    {
        _factory = new ApiFactory(fixture);
        await _factory.ResetAndSeedAsync();
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Theory]
    [InlineData("v1")]
    [InlineData("v2")]
    public async Task EachDocument_DeclaresTheBearerScheme(string version)
    {
        using var document = await DocumentAsync(version);

        var scheme = document.RootElement.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer");

        scheme.GetProperty("type").GetString().Should().Be("http");
        scheme.GetProperty("scheme").GetString().Should().Be("bearer");
        scheme.GetProperty("bearerFormat").GetString().Should().Be("JWT");
    }

    [Fact]
    public async Task ProtectedOperations_RequireTheBearerToken_AndAnonymousOnesDoNot()
    {
        using var document = await DocumentAsync("v1");
        var paths = document.RootElement.GetProperty("paths");

        HasSecurity(paths, "/api/v1/users", "get").Should().BeTrue();
        HasSecurity(paths, "/api/v1/users/{id}/lock", "post").Should().BeTrue();
        HasSecurity(paths, "/api/v1/auth/login", "post").Should().BeFalse();
        HasSecurity(paths, "/api", "get").Should().BeFalse();
    }

    [Fact]
    public async Task ProblemDetails_DeclaresTheExtensionsTheApiWrites()
    {
        using var document = await DocumentAsync("v1");

        var properties = document.RootElement.GetProperty("components").GetProperty("schemas")
            .GetProperty("ProblemDetails").GetProperty("properties");

        properties.TryGetProperty("errorCode", out _).Should().BeTrue();
        properties.TryGetProperty("traceId", out _).Should().BeTrue();
        properties.GetProperty("errors").GetProperty("additionalProperties")
            .GetProperty("items").GetProperty("type").GetString().Should().Be("string");
    }

    [Theory]
    [InlineData("v1", "UserDetailsResponse")]
    [InlineData("v1", "UserResponse")]
    [InlineData("v2", "UserResponse")]
    public async Task UserStatus_IsTheClosedSetOfStates(string version, string schema)
    {
        using var document = await DocumentAsync(version);

        var values = document.RootElement.GetProperty("components").GetProperty("schemas")
            .GetProperty(schema).GetProperty("properties").GetProperty("status").GetProperty("enum")
            .EnumerateArray().Select(value => value.GetString());

        values.Should().BeEquivalentTo("Pending", "Active", "Locked", "Deactivated");
    }

    [Fact]
    public async Task RateLimitedOperations_List429_AndOperationsWithABody_List413()
    {
        using var document = await DocumentAsync("v1");
        var paths = document.RootElement.GetProperty("paths");

        Responses(paths, "/api/v1/users", "get").TryGetProperty("429", out _).Should().BeTrue();
        Responses(paths, "/api/v1/users", "post").TryGetProperty("413", out _).Should().BeTrue();
        Responses(paths, "/api/v1/users", "get").TryGetProperty("413", out _).Should().BeFalse();
    }

    private async Task<JsonDocument> DocumentAsync(string version)
    {
        var json = await _factory.CreateClient().GetStringAsync($"/openapi/{version}.json");

        return JsonDocument.Parse(json);
    }

    private static bool HasSecurity(JsonElement paths, string path, string method) =>
        paths.GetProperty(path).GetProperty(method).TryGetProperty("security", out var security) &&
        security.GetArrayLength() > 0;

    private static JsonElement Responses(JsonElement paths, string path, string method) =>
        paths.GetProperty(path).GetProperty(method).GetProperty("responses");
}
