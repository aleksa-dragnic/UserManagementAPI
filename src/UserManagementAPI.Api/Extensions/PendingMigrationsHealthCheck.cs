using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

using UserManagementAPI.Infrastructure.Persistence;

namespace UserManagementAPI.Api.Extensions;

/// <summary>
/// Readiness fails while the database lacks a migration this build carries. A
/// deployed instance does not migrate on startup (docs/DATABASE.md), so code
/// can reach a database that is not ready for it: on 2026-11-24 the login
/// backoff went live before its table existed, and every login was a 500.
/// Render deploys only once /health/ready answers 200 (render.yaml), so with
/// this check the previous version keeps serving until the migrations are in.
/// The names of the missing migrations stay in the check's description, which
/// the endpoint does not write: the response says only Unhealthy.
/// </summary>
public sealed class PendingMigrationsHealthCheck(AppDbContext context) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext healthCheckContext,
        CancellationToken cancellationToken = default)
    {
        var pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();

        return pending.Count == 0
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy($"Not applied: {string.Join(", ", pending)}");
    }
}
