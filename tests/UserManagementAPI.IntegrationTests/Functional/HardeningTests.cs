using System.Net;
using System.Net.Http.Json;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using UserManagementAPI.Api.Contracts.V1;

namespace UserManagementAPI.IntegrationTests.Functional;

/// <summary>
/// The defences added in M6 PR24: a rate limit on the authentication endpoints,
/// security headers on every response, CORS closed to an origin nobody allowed,
/// a request body cap, and an append-only audit log.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class HardeningTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private ApiFactory _factory = null!;

    public async Task InitializeAsync()
    {
        // Two attempts per minute, so the third is refused without a test that
        // sits in a loop; one allowed origin, so CORS has something to reject.
        _factory = new ApiFactory(fixture, settings: new Dictionary<string, string?>
        {
            ["RateLimiting:Auth:PermitLimit"] = "2",
            ["RateLimiting:Auth:Window"] = "00:01:00",
            ["Cors:AllowedOrigins:0"] = "https://allowed.example.com"
        });

        await _factory.ResetAndSeedAsync();
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task ExceedingTheAuthPolicy_Returns429_WithRetryAfter()
    {
        var client = _factory.CreateClient();
        var credentials = new LoginRequest("nobody@example.com", "Wrong-Passw0rd!");

        var first = await client.PostAsJsonAsync("/api/v1/auth/login", credentials);
        var second = await client.PostAsJsonAsync("/api/v1/auth/login", credentials);
        var third = await client.PostAsJsonAsync("/api/v1/auth/login", credentials);

        first.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        second.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        third.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        third.Headers.RetryAfter.Should().NotBeNull();
        third.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task SecurityHeaders_ArePresentOnEveryResponse()
    {
        var response = await _factory.CreateClient().GetAsync("/health");

        response.Headers.GetValues("X-Content-Type-Options").Single().Should().Be("nosniff");
        response.Headers.GetValues("X-Frame-Options").Single().Should().Be("DENY");
        response.Headers.GetValues("Referrer-Policy").Single().Should().Be("no-referrer");
        response.Headers.GetValues("Content-Security-Policy").Single().Should().Contain("default-src 'none'");
    }

    [Fact]
    public async Task TheDocumentationGetsAPolicyThatLetsItLoad()
    {
        var response = await _factory.CreateClient().GetAsync("/openapi/v1.json");

        response.Headers.GetValues("Content-Security-Policy").Single()
            .Should().Contain("default-src 'self'").And.NotContain("default-src 'none'");
    }

    [Fact]
    public async Task ADisallowedOrigin_GetsNoCorsHeader_WhileAnAllowedOneDoes()
    {
        var allowed = await PreflightAsync("https://allowed.example.com");
        var disallowed = await PreflightAsync("https://someone-else.example.com");

        allowed.Headers.GetValues("Access-Control-Allow-Origin").Single()
            .Should().Be("https://allowed.example.com");
        disallowed.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();

        // Access-Control-Expose-Headers belongs to the actual response, not to the
        // preflight: the preflight says which headers may be sent, and only a real
        // response says which of its own headers a browser may read.
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/users");
        request.Headers.Add("Origin", "https://allowed.example.com");

        var actual = await _factory.CreateClient().SendAsync(request);

        actual.Headers.GetValues("Access-Control-Expose-Headers").Single()
            .Should().Contain("X-Pagination");
    }

    // The request body cap is not asserted here. MaxRequestBodySize is a Kestrel
    // limit and these tests run on TestServer, which is not Kestrel: an oversized
    // body reaches the action and is answered on its merits instead of with 413.
    // It is verified by hand against the running instance.

    [Fact]
    public async Task TheAuditLog_RefusesUpdatesAndDeletes()
    {
        var administrator = await _factory.CreateAdministratorClientAsync();
        await administrator.RegisterUserAsync("audited@example.com", "Audited-Passw0rd!");

        await using var context = fixture.CreateContext();

        var update = async () => await context.Database.ExecuteSqlRawAsync(
            "update audit_log set action = 'Tampered';");
        var delete = async () => await context.Database.ExecuteSqlRawAsync("delete from audit_log;");

        (await context.AuditLog.CountAsync()).Should().BeGreaterThan(0);
        (await update.Should().ThrowAsync<PostgresException>()).Which.MessageText.Should().Contain("append-only");
        await delete.Should().ThrowAsync<PostgresException>();
    }

    private Task<HttpResponseMessage> PreflightAsync(string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/users");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");

        return _factory.CreateClient().SendAsync(request);
    }
}