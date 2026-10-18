using System.Net;
using System.Net.Http.Json;

using UserManagementAPI.Api.Contracts.V1;

namespace UserManagementAPI.IntegrationTests.Functional;

/// <summary>
/// Optimistic concurrency on users over real HTTP (ADR 0021): the detail
/// carries a version, a write that sends it back in If-Match succeeds while it
/// is current and is refused with 412 once it is not, a write without If-Match
/// behaves as before, and a malformed If-Match is a 400.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class ConcurrencyTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private ApiFactory _factory = null!;
    private HttpClient _administrator = null!;
    private Guid _userId;

    public async Task InitializeAsync()
    {
        _factory = new ApiFactory(fixture);
        await _factory.ResetAndSeedAsync();
        _administrator = await _factory.CreateAdministratorClientAsync();
        _userId = await _administrator.RegisterUserAsync("versioned@example.com", "Versioned-Passw0rd!");
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task TheDetail_CarriesAVersion_ThatChangesOnEveryWrite()
    {
        var before = await DetailAsync();

        (await UpdateAsync(ifMatch: null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var after = await DetailAsync();

        before.Version.Should().NotBeNullOrEmpty();
        after.Version.Should().NotBe(before.Version);
    }

    [Fact]
    public async Task AWrite_WithTheCurrentVersion_Succeeds()
    {
        var current = await DetailAsync();

        var response = await UpdateAsync($"\"{current.Version}\"");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task AWrite_WithAStaleVersion_IsRefusedWith412_AndChangesNothing()
    {
        var stale = await DetailAsync();
        (await UpdateAsync($"\"{stale.Version}\"", lastName: "First")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await UpdateAsync($"\"{stale.Version}\"", lastName: "Second");

        response.StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);
        (await response.ErrorCodeAsync()).Should().Be("User.VersionMismatch");
        (await DetailAsync()).LastName.Should().Be("First");
    }

    [Fact]
    public async Task ALock_WithAStaleVersion_IsRefusedWith412()
    {
        var stale = await DetailAsync();
        (await UpdateAsync(ifMatch: null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/users/{_userId}/lock");
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{stale.Version}\"");
        var response = await _administrator.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);
    }

    [Theory]
    [InlineData("W/\"1\"")]
    [InlineData("1")]
    [InlineData("\"not-a-version\"")]
    public async Task AMalformedIfMatch_IsA400(string ifMatch)
    {
        var response = await UpdateAsync(ifMatch);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ErrorCodeAsync()).Should().Be("Request.InvalidIfMatch");
    }

    private async Task<UserDetailsResponse> DetailAsync() =>
        (await _administrator.GetFromJsonAsync<UserDetailsResponse>($"/api/v1/users/{_userId}"))!;

    private async Task<HttpResponseMessage> UpdateAsync(string? ifMatch, string lastName = "Renamed")
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/users/{_userId}")
        {
            Content = JsonContent.Create(new UpdateUserRequest("versioned@example.com", "Ana", lastName))
        };

        if (ifMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        return await _administrator.SendAsync(request);
    }
}
