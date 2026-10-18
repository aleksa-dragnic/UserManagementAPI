using System.Net;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

using UserManagementAPI.Api.Contracts.V1;

namespace UserManagementAPI.IntegrationTests.Functional;

/// <summary>
/// The auth rate limit partitions by the caller's address. X-Forwarded-For can
/// only be trusted from a proxy that sets it; trusted from anyone, a caller who
/// reaches the application directly can pick a new address for every request
/// and never be limited. The test server's connection has no address, which
/// ASP.NET Core does not check against the known proxies, so a startup filter
/// gives every request a public one, as a direct caller on the internet has.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class ForwardedHeadersTests(DatabaseFixture fixture)
{
    private static readonly IPAddress DirectCaller = IPAddress.Parse("198.51.100.7");

    [Fact]
    public async Task AForwardedFor_FromAnUntrustedCaller_DoesNotEscapeTheAuthLimit()
    {
        await using var factory = new ApiFactory(
            fixture,
            services => services.AddSingleton<IStartupFilter>(new RemoteAddress(DirectCaller)),
            new Dictionary<string, string?>
            {
                ["RateLimiting:Auth:PermitLimit"] = "2"
            });
        await factory.ResetAndSeedAsync();

        var statuses = await LoginFromThreeAddressesAsync(factory.CreateClient());

        statuses.Should().Contain(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task AForwardedFor_FromATrustedProxy_SeparatesTheCallers()
    {
        await using var factory = new ApiFactory(
            fixture,
            services => services.AddSingleton<IStartupFilter>(new RemoteAddress(DirectCaller)),
            new Dictionary<string, string?>
            {
                ["RateLimiting:Auth:PermitLimit"] = "2",
                ["ForwardedHeaders:TrustAllProxies"] = "true"
            });
        await factory.ResetAndSeedAsync();

        var statuses = await LoginFromThreeAddressesAsync(factory.CreateClient());

        statuses.Should().NotContain(HttpStatusCode.TooManyRequests);
    }

    private static async Task<List<HttpStatusCode>> LoginFromThreeAddressesAsync(HttpClient client)
    {
        var statuses = new List<HttpStatusCode>();

        foreach (var address in new[] { "203.0.113.1", "203.0.113.2", "203.0.113.3" })
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
            {
                Content = JsonContent.Create(new LoginRequest("nobody@example.com", "Wrong-Passw0rd!"))
            };
            request.Headers.Add("X-Forwarded-For", address);

            statuses.Add((await client.SendAsync(request)).StatusCode);
        }

        return statuses;
    }

    /// <summary>Runs ahead of the application's pipeline, so ahead of UseForwardedHeaders.</summary>
    private sealed class RemoteAddress(IPAddress address) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                context.Connection.RemoteIpAddress = address;
                return nextMiddleware(context);
            });

            next(app);
        };
    }
}
