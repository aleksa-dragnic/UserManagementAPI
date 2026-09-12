using System.Net;
using System.Net.Http.Json;

using UserManagementAPI.Api.Contracts.V1;
using UserManagementAPI.Domain.Roles;

namespace UserManagementAPI.IntegrationTests.Functional;

/// <summary>
/// The credential lifecycle over real HTTP: register, log in, call a protected
/// endpoint, refresh, replay a rotated token, log out, and fail to log in once
/// locked.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class AuthFlowTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private const string Email = "flow@example.com";

    private const string Password = "Flow-Passw0rd!";

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
    public async Task ReplayingARotatedRefreshToken_RevokesTheWholeChain()
    {
        await RegisterMemberAsync();
        var client = _factory.CreateClient();

        var first = await LoginAsync(client);
        var second = await RefreshAsync(client, first.RefreshToken);

        var replay = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshTokenRequest(first.RefreshToken));
        var afterReplay = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshTokenRequest(second.RefreshToken));

        second.AccessToken.Should().NotBe(first.AccessToken);
        replay.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await replay.ErrorCodeAsync()).Should().Be("Auth.RefreshTokenReused");
        afterReplay.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AfterLogout_TheRefreshTokenIsDead()
    {
        await RegisterMemberAsync();
        var client = _factory.CreateClient();
        var tokens = await LoginAsync(client);

        var logout = await client.PostAsJsonAsync("/api/v1/auth/logout", new RefreshTokenRequest(tokens.RefreshToken));
        var refresh = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshTokenRequest(tokens.RefreshToken));

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

    private static async Task<TokenResponse> LoginAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(Email, Password));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return (await response.Content.ReadFromJsonAsync<TokenResponse>())!;
    }

    private static async Task<TokenResponse> RefreshAsync(HttpClient client, string refreshToken)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshTokenRequest(refreshToken));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return (await response.Content.ReadFromJsonAsync<TokenResponse>())!;
    }
}