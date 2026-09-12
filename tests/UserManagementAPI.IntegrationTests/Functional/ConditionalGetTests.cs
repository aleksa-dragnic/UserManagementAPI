using System.Net;
using System.Net.Http.Json;
using System.Text;

using UserManagementAPI.Api.Caching;
using UserManagementAPI.Api.Contracts.V1;

namespace UserManagementAPI.IntegrationTests.Functional;

/// <summary>
/// Conditional GET by validation: an ETag on every read, 304 when the client
/// already holds the representation, 200 with the new one when it does not.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class ConditionalGetTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private ApiFactory _factory = null!;

    private HttpClient _administrator = null!;

    public async Task InitializeAsync()
    {
        _factory = new ApiFactory(fixture);
        await _factory.ResetAndSeedAsync();
        _administrator = await _factory.CreateAdministratorClientAsync();
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public void IdenticalPayloads_ProduceIdenticalETags_AndDifferentOnesDiffer()
    {
        var first = ETagGenerator.Generate(Encoding.UTF8.GetBytes("""{"id":1}"""));
        var again = ETagGenerator.Generate(Encoding.UTF8.GetBytes("""{"id":1}"""));
        var other = ETagGenerator.Generate(Encoding.UTF8.GetBytes("""{"id":2}"""));

        first.Should().Be(again).And.StartWith("\"").And.EndWith("\"");
        other.Should().NotBe(first);
    }

    [Fact]
    public async Task AMatchingIfNoneMatch_Returns304_WithAnEmptyBody()
    {
        var userId = await _administrator.RegisterUserAsync("cached@example.com", "Cached-Passw0rd!");

        var first = await _administrator.GetAsync($"/api/v1/users/{userId}");
        var etag = first.Headers.ETag!.Tag;

        var second = await GetWithIfNoneMatchAsync($"/api/v1/users/{userId}", etag);

        first.Headers.CacheControl!.Private.Should().BeTrue();
        first.Headers.CacheControl!.NoCache.Should().BeTrue();
        second.StatusCode.Should().Be(HttpStatusCode.NotModified);
        second.Headers.ETag!.Tag.Should().Be(etag);
        (await second.Content.ReadAsByteArrayAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task AStaleIfNoneMatch_Returns200_WithTheCurrentRepresentation()
    {
        var userId = await _administrator.RegisterUserAsync("stale@example.com", "Stale-Passw0rd!");
        var before = (await _administrator.GetAsync($"/api/v1/users/{userId}")).Headers.ETag!.Tag;

        var update = await _administrator.PutAsJsonAsync(
            $"/api/v1/users/{userId}",
            new UpdateUserRequest("stale@example.com", "Renamed", "User"));
        update.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var after = await GetWithIfNoneMatchAsync($"/api/v1/users/{userId}", before);

        after.StatusCode.Should().Be(HttpStatusCode.OK);
        after.Headers.ETag!.Tag.Should().NotBe(before);
        (await after.Content.ReadFromJsonAsync<UserDetailsResponse>())!.FirstName.Should().Be("Renamed");
    }

    [Fact]
    public async Task APutThatChangesNothing_LeavesTheETagAlone()
    {
        var userId = await _administrator.RegisterUserAsync("steady@example.com", "Steady-Passw0rd!", "Ana", "Petrović");
        var before = (await _administrator.GetAsync($"/api/v1/users/{userId}")).Headers.ETag!.Tag;

        var update = await _administrator.PutAsJsonAsync(
            $"/api/v1/users/{userId}",
            new UpdateUserRequest("STEADY@example.com", "Ana", "Petrović"));
        update.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var after = await GetWithIfNoneMatchAsync($"/api/v1/users/{userId}", before);

        after.StatusCode.Should().Be(HttpStatusCode.NotModified);
    }

    private Task<HttpResponseMessage> GetWithIfNoneMatchAsync(string path, string etag)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.TryAddWithoutValidation("If-None-Match", etag);

        return _administrator.SendAsync(request);
    }
}