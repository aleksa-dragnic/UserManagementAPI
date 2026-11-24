using System.Net;
using System.Net.Http.Json;

using UserManagementAPI.Api.Contracts.V1;

namespace UserManagementAPI.IntegrationTests.Functional;

/// <summary>
/// The login throttle over real HTTP (ADR 0024): after five wrong passwords
/// for one email, the next attempt is 429 with Retry-After and
/// Auth.TooManyFailedAttempts - with the right password too - and an unknown
/// email is refused the same way.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class LoginThrottleHttpTests(DatabaseFixture fixture) : IAsyncLifetime
{
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
    public async Task AfterFiveWrongPasswords_EvenTheRightOne_IsRefusedWith429AndRetryAfter()
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            (await LoginAsync(DatabaseFixture.AdministratorEmail, "not-the-password")).StatusCode
                .Should().Be(HttpStatusCode.Unauthorized);
        }

        var refused = await LoginAsync(DatabaseFixture.AdministratorEmail, DatabaseFixture.AdministratorPassword);

        refused.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        (await refused.ErrorCodeAsync()).Should().Be("Auth.TooManyFailedAttempts");
        refused.Headers.RetryAfter!.Delta.Should().Be(TimeSpan.FromSeconds(1));
        refused.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task AnUnknownEmail_IsThrottledExactlyLikeAKnownOne()
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            (await LoginAsync("nobody@example.com", "anything")).StatusCode
                .Should().Be(HttpStatusCode.Unauthorized);
        }

        var refused = await LoginAsync("nobody@example.com", "anything");

        refused.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        (await refused.ErrorCodeAsync()).Should().Be("Auth.TooManyFailedAttempts");
    }

    private Task<HttpResponseMessage> LoginAsync(string email, string password) =>
        _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));
}
