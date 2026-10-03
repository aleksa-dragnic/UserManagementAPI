using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.Net.Http.Headers;

using UserManagementAPI.Api.Contracts.V1;

namespace UserManagementAPI.IntegrationTests.Functional;

/// <summary>
/// The refresh token's transport, ADR 0019: an HttpOnly cookie scoped to the
/// auth endpoints, set by login and refresh, read by refresh and logout. The
/// body carries the access token and nothing else.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class RefreshCookieTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private const string RefreshPath = "/api/v1/auth/refresh";

    private const string LogoutPath = "/api/v1/auth/logout";

    private ApiFactory _factory = null!;

    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _factory = new ApiFactory(fixture);
        await _factory.ResetAndSeedAsync();
        _client = _factory.CreateClientWithExplicitCookies();
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task Login_SetsTheRefreshCookie_WithEveryAttribute()
    {
        var cookie = (await LoginAsync()).RefreshCookie();

        cookie.Should().NotBeNull();
        cookie!.Value.Value.Should().NotBeNullOrEmpty();
        cookie.HttpOnly.Should().BeTrue();
        cookie.Secure.Should().BeTrue();
        cookie.SameSite.Should().Be(SameSiteMode.Strict);
        cookie.Path.Value.Should().Be("/api/v1/auth");
        cookie.Domain.HasValue.Should().BeFalse("a host-only cookie carries no Domain attribute");
        cookie.MaxAge.Should().NotBeNull("without Max-Age the cookie would die with the browser while the token lives on");
        cookie.MaxAge!.Value.Should().BeCloseTo(TimeSpan.FromDays(7), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task LoginAndRefreshBodies_CarryTheAccessTokenAndItsExpiryOnly()
    {
        var login = await LoginAsync();
        var refresh = await _client.PostWithRefreshCookieAsync(RefreshPath, login.RefreshTokenFromCookie());

        refresh.StatusCode.Should().Be(HttpStatusCode.OK);
        (await PropertyNamesAsync(login)).Should().BeEquivalentTo("accessToken", "accessTokenExpiresAtUtc");
        (await PropertyNamesAsync(refresh)).Should().BeEquivalentTo("accessToken", "accessTokenExpiresAtUtc");
    }

    [Fact]
    public async Task RefreshWithTheCookie_Is200_AndRotatesIt()
    {
        var first = (await LoginAsync()).RefreshTokenFromCookie();

        var refresh = await _client.PostWithRefreshCookieAsync(RefreshPath, first);
        var second = refresh.RefreshCookie();

        refresh.StatusCode.Should().Be(HttpStatusCode.OK);
        second.Should().NotBeNull();
        second!.Value.Value.Should().NotBeNullOrEmpty().And.NotBe(first);
        second.Path.Value.Should().Be("/api/v1/auth");
        second.HttpOnly.Should().BeTrue();
        second.MaxAge.Should().NotBeNull();
    }

    [Fact]
    public async Task RefreshWithoutACookie_Is401()
    {
        var refresh = await _client.PostWithRefreshCookieAsync(RefreshPath, refreshToken: null);

        refresh.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await refresh.ErrorCodeAsync()).Should().Be("Auth.InvalidRefreshToken");
    }

    [Fact]
    public async Task RefreshWithABodyTokenAndNoCookie_Is401()
    {
        var refreshToken = (await LoginAsync()).RefreshTokenFromCookie();

        var refresh = await _client.PostAsJsonAsync(RefreshPath, new { refreshToken });

        refresh.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_ClearsTheCookie_OnTheSamePath()
    {
        var refreshToken = (await LoginAsync()).RefreshTokenFromCookie();

        var logout = await _client.PostWithRefreshCookieAsync(LogoutPath, refreshToken);
        var cleared = logout.RefreshCookie();

        logout.StatusCode.Should().Be(HttpStatusCode.NoContent);
        cleared.Should().NotBeNull();
        cleared!.Value.Value.Should().BeNullOrEmpty();
        cleared.Expires.Should().BeBefore(DateTimeOffset.UtcNow);
        cleared.Path.Value.Should().Be("/api/v1/auth", "a clear on any other path leaves the cookie in place");
    }

    [Fact]
    public async Task LogoutWithoutACookie_Is204_AndStillClearsIt()
    {
        var logout = await _client.PostWithRefreshCookieAsync(LogoutPath, refreshToken: null);
        var cleared = logout.RefreshCookie();

        logout.StatusCode.Should().Be(HttpStatusCode.NoContent);
        cleared.Should().NotBeNull();
        cleared!.Value.Value.Should().BeNullOrEmpty();
    }

    private async Task<HttpResponseMessage> LoginAsync()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(DatabaseFixture.AdministratorEmail, DatabaseFixture.AdministratorPassword));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return response;
    }

    private static async Task<List<string>> PropertyNamesAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return document.RootElement.EnumerateObject().Select(property => property.Name).ToList();
    }
}