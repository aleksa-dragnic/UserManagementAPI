using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Testcontainers.PostgreSql;

using UserManagementAPI.Application;
using UserManagementAPI.Application.Abstractions;
using UserManagementAPI.Infrastructure;
using UserManagementAPI.Infrastructure.Persistence;
using UserManagementAPI.Infrastructure.Persistence.Interceptors;

namespace UserManagementAPI.IntegrationTests;

/// <summary>
/// Starts one real PostgreSQL for the whole test run and migrates it. Real,
/// because the in-memory provider enforces no constraint and translates no SQL,
/// so it passes tests that fail in production (ADR 0012).
/// </summary>
public sealed class DatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    /// <summary>A context with no interceptor, for plain persistence assertions.</summary>
    public AppDbContext CreateContext() => new(BuildOptions(null));

    /// <summary>A context wired for domain event dispatch, with the publisher under test.</summary>
    public AppDbContext CreateContext(IDomainEventPublisher publisher) => new(BuildOptions(publisher));

    /// <summary>
    /// The production wiring — AddApplication plus AddInfrastructure — against
    /// the container, so a test can prove the pieces cooperate the way they will
    /// in the running API. The outbox is configured to retry without delay and
    /// give up after three attempts, so a retry test finishes in milliseconds.
    /// </summary>
    public ServiceProvider CreateServiceProvider(Action<IServiceCollection>? configure = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = ConnectionString,
                ["Outbox:BaseBackoff"] = "00:00:00",
                ["Outbox:MaxAttempts"] = "3"
            })
            .Build();

        var services = new ServiceCollection();

        services.AddLogging();
        services.AddApplication();
        services.AddInfrastructure(configuration);

        configure?.Invoke(services);

        return services.BuildServiceProvider();
    }

    /// <summary>Leaves the schema in place and removes the rows, so tests do not see each other's data.</summary>
    public async Task ResetAsync()
    {
        await using var context = CreateContext();

        await context.Database.ExecuteSqlRawAsync(
            "truncate table outbox_messages, user_roles, role_permissions, users, roles, permissions restart identity cascade;");
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    private DbContextOptions<AppDbContext> BuildOptions(IDomainEventPublisher? publisher)
    {
        var builder = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .UseSnakeCaseNamingConvention();

        if (publisher is not null)
        {
            builder.AddInterceptors(new DomainEventDispatchInterceptor(publisher));
        }

        return builder.Options;
    }
}