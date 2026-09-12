using System.Net;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;

namespace UserManagementAPI.IntegrationTests.Functional;

/// <summary>
/// URL-segment versioning: v1 and v2 of the same route side by side, each with
/// its own contract and its own OpenAPI document.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class VersioningTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private ApiFactory _factory = null!;

    private HttpClient _administrator = null!;

    private Guid _administratorId;

    public async Task InitializeAsync()
    {
        _factory = new ApiFactory(fixture);
        await _factory.ResetAndSeedAsync();
        _administrator = await _factory.CreateAdministratorClientAsync();

        await using var context = fixture.CreateContext();
        _administratorId = await context.Users
            .Where(user => user.Email.Value == DatabaseFixture.AdministratorEmail)
            .Select(user => user.Id)
            .SingleAsync();
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task V1AndV2OfTheSameRoute_ReturnTheirOwnShapes()
    {
        using var v1 = await ReadJsonAsync($"/api/v1/users/{_administratorId}");
        using var v2 = await ReadJsonAsync($"/api/v2/users/{_administratorId}");

        v1.RootElement.GetProperty("firstName").GetString().Should().Be("System");
        v1.RootElement.TryGetProperty("displayName", out _).Should().BeFalse();

        v2.RootElement.GetProperty("displayName").GetString().Should().Be("System Administrator");
        v2.RootElement.TryGetProperty("firstName", out _).Should().BeFalse();
    }

    /// <summary>
    /// 404, not the 400 the M5 guide expected: with the version in the URL
    /// segment, Asp.Versioning treats an unsupported version as an address that
    /// does not exist and always answers 404.
    /// </summary>
    [Fact]
    public async Task AnUnsupportedVersion_IsNotFound()
    {
        var response = await _administrator.GetAsync($"/api/v3/users/{_administratorId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Responses_ReportTheSupportedVersions()
    {
        var response = await _administrator.GetAsync($"/api/v1/users/{_administratorId}");

        response.Headers.GetValues("api-supported-versions").Single()
            .Split(',', StringSplitOptions.TrimEntries)
            .Should().BeEquivalentTo("1.0", "2.0");
    }

    [Fact]
    public async Task EachVersion_HasItsOwnOpenApiDocument()
    {
        var client = _factory.CreateClient();

        var v1 = await client.GetStringAsync("/openapi/v1.json");
        var v2 = await client.GetStringAsync("/openapi/v2.json");

        v1.Should().Contain("/api/v1/users").And.NotContain("/api/v2/");
        v2.Should().Contain("/api/v2/users/{id}").And.NotContain("/api/v1/");
    }

    private async Task<JsonDocument> ReadJsonAsync(string path)
    {
        var response = await _administrator.GetAsync(path);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }
}