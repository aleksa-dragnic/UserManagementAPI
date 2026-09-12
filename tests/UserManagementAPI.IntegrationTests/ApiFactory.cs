using System.Net.Http.Headers;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using UserManagementAPI.Api.Contracts.V1;
using UserManagementAPI.Infrastructure.Persistence.Seed;

namespace UserManagementAPI.IntegrationTests;

/// <summary>
/// The real application over real HTTP, against the shared Testcontainers
/// database. Program is partial and public (M0 PR3) precisely so this can exist.
/// Pulled forward from M6 PR23 because M4 needed one functional test — the
/// seeded Member must get a 403 on a write endpoint — and PR23 extended it with
/// per-test service replacement and setting overrides.
/// </summary>
public sealed class ApiFactory(
    DatabaseFixture fixture,
    Action<IServiceCollection>? configureServices = null,
    IDictionary<string, string?>? settings = null) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        // Same settings the wiring tests use: the container, a signing key, the
        // seeded administrator. UseSetting, not ConfigureAppConfiguration: with
        // minimal hosting, Program.cs reads the connection string
        // (AddInfrastructure) and the signing key (AddApiAuthentication) before
        // builder.Build(), and ConfigureAppConfiguration is only applied at
        // Build(). UseSetting reaches WebApplication.CreateBuilder as host
        // configuration, ahead of every read, and wins over appsettings.json
        // and the user secrets on the machine.
        var values = fixture.DefaultSettings();

        foreach (var (key, value) in settings ?? new Dictionary<string, string?>())
        {
            values[key] = value;
        }

        foreach (var (key, value) in values)
        {
            builder.UseSetting(key, value);
        }

        // ConfigureTestServices runs after Program.cs has registered everything,
        // so a replacement here wins. It is how a test makes a dependency throw
        // without a test-only endpoint in the production code.
        if (configureServices is not null)
        {
            builder.ConfigureTestServices(configureServices);
        }
    }

    /// <summary>
    /// Other test classes in the collection truncate the database, taking the
    /// seeded administrator with it. Every functional test starts from a clean,
    /// freshly seeded database.
    /// </summary>
    public async Task ResetAndSeedAsync()
    {
        await fixture.ResetAsync();

        await using var scope = Services.CreateAsyncScope();
        await ActivatorUtilities.CreateInstance<DatabaseSeeder>(scope.ServiceProvider).SeedAsync();
    }

    /// <summary>A client with a bearer token for the given credentials.</summary>
    public async Task<HttpClient> CreateClientAsAsync(string email, string password)
    {
        var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));
        response.EnsureSuccessStatusCode();

        var tokens = await response.Content.ReadFromJsonAsync<TokenResponse>();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);

        return client;
    }

    public Task<HttpClient> CreateAdministratorClientAsync() =>
        CreateClientAsAsync(DatabaseFixture.AdministratorEmail, DatabaseFixture.AdministratorPassword);
}