using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using UserManagementAPI.Api.Hateoas;

namespace UserManagementAPI.IntegrationTests.Functional;

/// <summary>
/// Links on request: plain JSON by default, links when the client sends the
/// vendor media type, 406 for a type the API does not produce. Plus the root
/// document, OPTIONS and HEAD.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class HypermediaTests(DatabaseFixture fixture) : IAsyncLifetime
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
    public async Task WithoutTheVendorMediaType_TheResponseIsPlainJson_WithNoLinks()
    {
        var response = await _administrator.GetAsync($"/api/v1/users/{_administratorId}");

        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.TryGetProperty("links", out _).Should().BeFalse();
    }

    [Fact]
    public async Task WithTheVendorMediaType_TheUserCarriesLinks_AndEachGetLinkResolves()
    {
        var response = await SendAsync(HttpMethod.Get, $"/api/v1/users/{_administratorId}", HateoasMediaTypes.Hateoas);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be(HateoasMediaTypes.Hateoas);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("value").GetProperty("email").GetString()
            .Should().Be(DatabaseFixture.AdministratorEmail);

        var links = ReadLinks(body.RootElement);
        links.Select(link => link.Rel).Should().BeEquivalentTo("self", "update", "lock", "unlock", "assign-role");
        links.Should().OnlyContain(link => link.Href.StartsWith($"/api/v1/users/{_administratorId}"));

        foreach (var link in links.Where(link => link.Method == "GET"))
        {
            (await _administrator.GetAsync(link.Href)).StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task TheCollection_LinksToTheNextPage_AndTheLinkWorks()
    {
        await _administrator.RegisterUserAsync("second@example.com", "Second-Passw0rd!");

        var first = await SendAsync(HttpMethod.Get, "/api/v1/users?pageSize=1", HateoasMediaTypes.Hateoas);
        using var firstBody = JsonDocument.Parse(await first.Content.ReadAsStringAsync());
        var next = ReadLinks(firstBody.RootElement).Single(link => link.Rel == "next");

        var second = await SendAsync(HttpMethod.Get, next.Href, HateoasMediaTypes.Hateoas);
        using var secondBody = JsonDocument.Parse(await second.Content.ReadAsStringAsync());

        next.Href.Should().Contain("pageNumber=2").And.Contain("pageSize=1");
        secondBody.RootElement.GetProperty("value")[0].GetProperty("value").GetProperty("email").GetString()
            .Should().Be("second@example.com");
        ReadLinks(secondBody.RootElement).Select(link => link.Rel).Should().Contain("previous").And.NotContain("next");
    }

    [Fact]
    public async Task AnUnknownVendorMediaType_Returns406()
    {
        var response = await SendAsync(HttpMethod.Get, "/api/v1/users", "application/vnd.someone-else+json");

        response.StatusCode.Should().Be(HttpStatusCode.NotAcceptable);
    }

    [Fact]
    public async Task TheRootDocument_IsAnonymous_AndLinksToTheTopLevelResources()
    {
        var anonymous = _factory.CreateClient();

        var response = await anonymous.GetAsync("/api");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var links = ReadLinks(body.RootElement);

        links.Should().Contain(new Link("/api", "self", "GET"));
        links.Should().Contain(new Link("/api/v1/users", "users", "GET"));
        links.Should().Contain(new Link("/api/v1/roles", "roles", "GET"));
        links.Should().Contain(new Link("/api/v1/auth/login", "login", "POST"));
    }

    [Fact]
    public async Task Options_ListsTheMethodsTheCollectionAllows()
    {
        var response = await _administrator.SendAsync(new HttpRequestMessage(HttpMethod.Options, "/api/v1/users"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.Allow.Should().BeEquivalentTo("GET", "HEAD", "POST", "OPTIONS");
    }

    [Fact]
    public async Task Head_ReturnsTheHeadersOfTheGet_WithNoBody()
    {
        var get = await _administrator.GetAsync($"/api/v1/users/{_administratorId}");
        var head = await _administrator.SendAsync(new HttpRequestMessage(HttpMethod.Head, $"/api/v1/users/{_administratorId}"));

        head.StatusCode.Should().Be(HttpStatusCode.OK);
        head.Headers.ETag.Should().Be(get.Headers.ETag);
        (await head.Content.ReadAsByteArrayAsync()).Should().BeEmpty();
    }

    private Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string accept)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(accept));

        return _administrator.SendAsync(request);
    }

    private static List<Link> ReadLinks(JsonElement resource) =>
        resource.GetProperty("links")
            .EnumerateArray()
            .Select(link => new Link(
                link.GetProperty("href").GetString()!,
                link.GetProperty("rel").GetString()!,
                link.GetProperty("method").GetString()!))
            .ToList();
}