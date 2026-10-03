namespace UserManagementAPI.IntegrationTests.Functional;

/// <summary>
/// CORS as a browser client on another origin needs it (ADR 0019): its exact
/// origin allowed with credentials, so the refresh cookie travels, and the
/// correlation id readable, so the client can show the id a log line carries.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class CorsCredentialsTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private const string ConsoleOrigin = "http://localhost:5173";

    private ApiFactory _factory = null!;

    public async Task InitializeAsync()
    {
        _factory = new ApiFactory(fixture, settings: new Dictionary<string, string?>
        {
            ["Cors:AllowedOrigins:0"] = ConsoleOrigin,
            ["Cors:AllowCredentials"] = "true"
        });

        await _factory.ResetAndSeedAsync();
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task APreflightFromTheConsoleOrigin_AllowsCredentials()
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/auth/refresh");
        request.Headers.Add("Origin", ConsoleOrigin);
        request.Headers.Add("Access-Control-Request-Method", "POST");

        var preflight = await _factory.CreateClient().SendAsync(request);

        preflight.Headers.GetValues("Access-Control-Allow-Origin").Single().Should().Be(ConsoleOrigin);
        preflight.Headers.GetValues("Access-Control-Allow-Credentials").Single().Should().Be("true");
    }

    [Fact]
    public async Task TheCorrelationId_IsAnExposedHeader()
    {
        // Access-Control-Expose-Headers belongs to the actual response, not to
        // the preflight.
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/users");
        request.Headers.Add("Origin", ConsoleOrigin);

        var response = await _factory.CreateClient().SendAsync(request);

        response.Headers.GetValues("Access-Control-Expose-Headers").Single()
            .Split(',', StringSplitOptions.TrimEntries)
            .Should().Contain("X-Correlation-Id");
    }
}