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
    /// In production both default to off. Applying a migration on boot means
    /// every replica races to run it, a failed migration becomes a crash loop,
    /// and a schema change ships silently with a deploy. There it is a
    /// deliberate step, run once, before the new version starts.
    ///
    /// Each can be switched on explicitly — Database__SeedOnStartup is how the
    /// demo instance gets its administrator and its read-only account the first
    /// time it boots, and is turned off again afterwards.
    /// </summary>
    public static async Task MigrateAndSeedAsync(this WebApplication app)
    {
        var migrate = app.Configuration.GetValue("Database:MigrateOnStartup", !app.Environment.IsProduction());
        var seed = app.Configuration.GetValue("Database:SeedOnStartup", !app.Environment.IsProduction());

        if (!migrate && !seed)
        {
            return;
        }

        await using var scope = app.Services.CreateAsyncScope();

        if (migrate)
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await context.Database.MigrateAsync();
        }

        if (seed)
        {
            var seeder = ActivatorUtilities.CreateInstance<DatabaseSeeder>(scope.ServiceProvider);
            await seeder.SeedAsync();
        }
    }
}