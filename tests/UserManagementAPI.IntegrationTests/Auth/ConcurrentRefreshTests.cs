using System.Net;
using System.Net.Http.Json;

using UserManagementAPI.Api.Contracts.V1;

namespace UserManagementAPI.IntegrationTests.Auth;

/// <summary>
/// Two requests presenting the same refresh token at the same moment. Before
/// the concurrency token on refresh_tokens both could rotate it and both walk
/// away with a live chain; now the loser is told the row moved under it.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class ConcurrentRefreshTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private ApiFactory _factory = null!;

    public async Task InitializeAsync()
    {
        _factory = new ApiFactory(fixture);
        await _factory.ResetAndSeedAsync();
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task OnlyOneOfTwoSimultaneousRefreshes_Succeeds()
    {
        var client = _factory.CreateClient();

        var login = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(DatabaseFixture.AdministratorEmail, DatabaseFixture.AdministratorPassword));
        var tokens = (await login.Content.ReadFromJsonAsync<TokenResponse>())!;

        var body = new RefreshTokenRequest(tokens.RefreshToken);

        var responses = await Task.WhenAll(
            client.PostAsJsonAsync("/api/v1/auth/refresh", body),
            client.PostAsJsonAsync("/api/v1/auth/refresh", body));

        responses.Count(response => response.StatusCode == HttpStatusCode.OK).Should().Be(1);

        var loser = responses.Single(response => response.StatusCode != HttpStatusCode.OK);

        // Either the second request read the row before the first committed and
        // lost the concurrency check (409), or it read the rotated row and was
        // treated as a replay (401, chain revoked). Both are correct refusals;
        // what must never happen is two live chains from one token.
        loser.StatusCode.Should().BeOneOf(HttpStatusCode.Conflict, HttpStatusCode.Unauthorized);
    }
}