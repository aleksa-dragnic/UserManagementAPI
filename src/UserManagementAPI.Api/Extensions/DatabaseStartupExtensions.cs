using Microsoft.EntityFrameworkCore;

using UserManagementAPI.Infrastructure.Persistence;
using UserManagementAPI.Infrastructure.Persistence.Seed;

namespace UserManagementAPI.Api.Extensions;

public static class DatabaseStartupExtensions
{
    /// <summary>
    /// Migrates and seeds on startup outside production, so "docker compose up"
    /// and a fresh clone both produce a working database with no extra step.
    ///
    /// In production this does nothing. Applying a migration on boot means every
    /// replica races to run it, a failed migration becomes a crash loop, and a
    /// schema change ships silently with a deploy. There it is a deliberate step
    /// run once, by a person or a pipeline, before the new version starts.
    /// </summary>
    public static async Task MigrateAndSeedAsync(this WebApplication app)
    {
        if (app.Environment.IsProduction())
        {
            return;
        }

        await using var scope = app.Services.CreateAsyncScope();

        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await context.Database.MigrateAsync();

        var seeder = ActivatorUtilities.CreateInstance<DatabaseSeeder>(scope.ServiceProvider);
        await seeder.SeedAsync();
    }
}