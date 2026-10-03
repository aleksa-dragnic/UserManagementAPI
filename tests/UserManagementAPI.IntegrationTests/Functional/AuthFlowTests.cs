using System.Net;
using System.Net.Http.Json;

using Microsoft.IdentityModel.JsonWebTokens;

using UserManagementAPI.Api.Contracts.V1;
using UserManagementAPI.Domain.Roles;

namespace UserManagementAPI.IntegrationTests.Functional;

/// <summary>
/// The credential lifecycle over real HTTP: register, log in, call a protected
/// endpoint, refresh, replay a rotated token, log out, and fail to log in once
/// locked. The refresh token travels in the umapi_rt cookie (ADR 0019).
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class AuthFlowTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private const string Email = "flow@example.com";

    private const string Password = "Flow-Passw0rd!";

    private const string RefreshPath = "/api/v1/auth/refresh";

    private const string LogoutPath = "/api/v1/auth/logout";

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
    public async Task Register_LogIn_CallAProtectedEndpoint()
    {
        var userId = await RegisterMemberAsync();

        var client = await _factory.CreateClientAsAsync(Email, Password);
        var response = await client.GetAsync($"/api/v1/users/{userId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task WithoutAToken_AProtectedEndpointIs401()
    {
        var userId = await RegisterMemberAsync();

        var response = await _factory.CreateClient().GetAsync($"/api/v1/users/{userId}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ReplayingARotatedRefreshToken_RevokesEverySessionOfTheAccount_AndClearsTheCookie()
    {
        await RegisterMemberAsync();
        var client = _factory.CreateClientWithExplicitCookies();

        var first = await LoginAsync(client);
        var otherSession = await LoginAsync(client);
        var second = await RefreshAsync(client, first);

        var replay = await client.PostWithRefreshCookieAsync(RefreshPath, first);
        var afterReplay = await client.PostWithRefreshCookieAsync(RefreshPath, second);
        var otherAfterReplay = await client.PostWithRefreshCookieAsync(RefreshPath, otherSession);

        second.Should().NotBe(first);
        replay.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await replay.ErrorCodeAsync()).Should().Be("Auth.RefreshTokenReused");

        var cleared = replay.RefreshCookie();
        cleared.Should().NotBeNull("reuse detection clears the cookie as well as revoking");
        cleared!.Value.Value.Should().BeNullOrEmpty();

        // Not one chain: every session of the account, including one that was
        // never rotated.
        afterReplay.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        otherAfterReplay.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AfterLogout_TheRefreshTokenIsDead()
    {
        await RegisterMemberAsync();
        var client = _factory.CreateClientWithExplicitCookies();
        var refreshToken = await LoginAsync(client);

        var logout = await client.PostWithRefreshCookieAsync(LogoutPath, refreshToken);
        var refresh = await client.PostWithRefreshCookieAsync(RefreshPath, refreshToken);

        logout.StatusCode.Should().Be(HttpStatusCode.NoContent);
        refresh.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ALockedUser_CannotLogIn()
    {
        var userId = await RegisterMemberAsync();

        var locked = await _administrator.PostAsync($"/api/v1/users/{userId}/lock", null);
        var login = await _administrator.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(Email, Password));

        locked.StatusCode.Should().Be(HttpStatusCode.NoContent);
        login.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await login.ErrorCodeAsync()).Should().Be("Auth.AccountLocked");
    }

    [Fact]
    public async Task AnAdministrator_CannotLockTheirOwnAccount()
    {
        var accessToken = _administrator.DefaultRequestHeaders.Authorization!.Parameter!;
        var administratorId = Guid.Parse(new JsonWebTokenHandler().ReadJsonWebToken(accessToken).Subject);

        var locked = await _administrator.PostAsync($"/api/v1/users/{administratorId}/lock", null);
        var login = await _factory.CreateClientWithExplicitCookies().PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(DatabaseFixture.AdministratorEmail, DatabaseFixture.AdministratorPassword));

        locked.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await locked.ErrorCodeAsync()).Should().Be("User.CannotLockSelf");
        login.StatusCode.Should().Be(HttpStatusCode.OK, "the refused lock left the account able to sign in");
    }

    [Fact]
    public async Task AWrongPassword_AndAnUnknownEmail_AnswerTheSameWay()
    {
        await RegisterMemberAsync();
        var client = _factory.CreateClient();

        var wrongPassword = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(Email, "Wrong-Passw0rd!"));
        var unknownEmail = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("nobody@example.com", Password));

        wrongPassword.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        unknownEmail.StatusCode.Should().Be(wrongPassword.StatusCode);
        (await unknownEmail.ErrorCodeAsync()).Should().Be(await wrongPassword.ErrorCodeAsync());
    }

    private async Task<Guid> RegisterMemberAsync()
    {
        var userId = await _administrator.RegisterUserAsync(Email, Password);
        await _administrator.AssignRoleAsync(userId, await fixture.RoleIdAsync(RoleNames.Member));

        return userId;
    }

    /// <summary>Logs the member in and returns the refresh token its cookie carries.</summary>
    private static async Task<string> LoginAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(Email, Password));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return response.RefreshTokenFromCookie();
    }

    /// <summary>Exchanges a refresh token and returns the one the new cookie carries.</summary>
    private static async Task<string> RefreshAsync(HttpClient client, string refreshToken)
    {
        var response = await client.PostWithRefreshCookieAsync(RefreshPath, refreshToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return response.RefreshTokenFromCookie();
    }
}