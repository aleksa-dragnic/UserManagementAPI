using System.Net;

using Microsoft.EntityFrameworkCore;

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

    [Theory]
    [InlineData("has spaces in it")]
    [InlineData("semi;colon")]
    [InlineData("an-id-that-is-much-longer-than-the-sixty-four-characters-allowed-for-one")]
    public async Task AMalformedCorrelationIdFromTheClient_IsReplaced(string fromClient)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.HeaderName, fromClient);

        var response = await _factory.CreateClient().SendAsync(request);

        response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single()
            .Should().NotBe(fromClient).And.NotBeNullOrWhiteSpace();
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

    [Fact]
    public async Task Readiness_FailsWhileAMigrationIsUnapplied_AndLivenessDoesNot()
    {
        // As on 2026-11-24: a build whose newest migration the database lacks.
        await using var context = fixture.CreateContext();
        var newest = (await context.Database.GetAppliedMigrationsAsync()).Last();
        var productVersion = await context.Database
            .SqlQuery<string>($"SELECT product_version AS \"Value\" FROM \"__EFMigrationsHistory\" WHERE migration_id = {newest}")
            .SingleAsync();
        await context.Database.ExecuteSqlAsync($"DELETE FROM \"__EFMigrationsHistory\" WHERE migration_id = {newest}");

        try
        {
            var client = _factory.CreateClient();

            (await client.GetAsync("/health/ready")).StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
            (await client.GetAsync("/health/live")).StatusCode.Should().Be(HttpStatusCode.OK);
        }
        finally
        {
            await context.Database.ExecuteSqlAsync(
                $"INSERT INTO \"__EFMigrationsHistory\" (migration_id, product_version) VALUES ({newest}, {productVersion})");
        }

        (await _factory.CreateClient().GetAsync("/health/ready")).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
