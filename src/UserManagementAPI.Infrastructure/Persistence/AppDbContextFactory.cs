using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace UserManagementAPI.Infrastructure.Persistence;

/// <summary>
/// Lets "dotnet ef" build a context without starting the API. It reads the
/// Migrations connection string, which must be the DIRECT Neon endpoint: the
/// pooler runs in transaction mode and does not support the session-level
/// operations EF Core uses while applying a migration.
/// </summary>
internal sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets(typeof(AppDbContextFactory).Assembly, optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("Migrations")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:Migrations is not configured. Set it with dotnet user-secrets " +
                "and use the direct Neon host, without '-pooler'.");

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsAssembly(typeof(AppDbContextFactory).Assembly.FullName))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new AppDbContext(options);
    }
}