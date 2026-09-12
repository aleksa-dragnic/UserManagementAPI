using System.Net;

using UserManagementAPI.Api.Middleware;

namespace UserManagementAPI.IntegrationTests.Functional;

/// <summary>
/// What an operator relies on: a correlation id on every response, and health
/// endpoints that separate "the process is alive" from "it can serve traffic".
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class ObservabilityTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private ApiFactory _factory = null!;

    public async Task InitializeAsync()
    {
        _factory = new ApiFactory(fixture);
        await _factory.ResetAndSeedAsync();
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task EveryResponse_CarriesACorrelationId()
    {
        var response = await _factory.CreateClient().GetAsync("/health/live");

        response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single()
            .Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ACorrelationIdFromTheClient_IsEchoedBack()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, "test-correlation-id");

        var response = await _factory.CreateClient().SendAsync(request);

        response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single()
            .Should().Be("test-correlation-id");
    }

    [Fact]
    public async Task LivenessAndReadiness_AreBothHealthy_WithTheDatabaseUp()
    {
        var client = _factory.CreateClient();

        var live = await client.GetAsync("/health/live");
        var ready = await client.GetAsync("/health/ready");
        var legacy = await client.GetAsync("/health");

        live.StatusCode.Should().Be(HttpStatusCode.OK);
        ready.StatusCode.Should().Be(HttpStatusCode.OK);
        legacy.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}