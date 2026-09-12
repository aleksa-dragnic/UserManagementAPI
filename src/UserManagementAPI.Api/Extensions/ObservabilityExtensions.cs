using Microsoft.Extensions.Diagnostics.HealthChecks;

using Npgsql;

using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using UserManagementAPI.Api.Middleware;
using UserManagementAPI.Infrastructure.Persistence;

namespace UserManagementAPI.Api.Extensions;

/// <summary>
/// Traces and metrics through OpenTelemetry, and health checks that say
/// something useful to an orchestrator.
///
/// The exporter is configured only when OTEL_EXPORTER_OTLP_ENDPOINT is set, so
/// a plain "dotnet run" does not spend every request trying to reach a
/// collector that is not there. The compose stack sets it.
/// </summary>
public static class ObservabilityExtensions
{
    public const string ServiceName = "UserManagementAPI";

    public static IServiceCollection AddObservability(this IServiceCollection services, IConfiguration configuration)
    {
        var otlpEndpoint = configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];

        services
            .AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(ServiceName))
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        // Health probes and the documentation would otherwise be
                        // most of the traces and none of the interesting ones.
                        options.Filter = context =>
                            !context.Request.Path.StartsWithSegments("/health") &&
                            !context.Request.Path.StartsWithSegments("/scalar") &&
                            !context.Request.Path.StartsWithSegments("/openapi");

                        options.RecordException = true;
                    })
                    .AddHttpClientInstrumentation()
                    .AddNpgsql();

                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                {
                    tracing.AddOtlpExporter();
                }
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();

                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                {
                    metrics.AddOtlpExporter();
                }
            });

        // Liveness answers "is this process alive": no dependency is checked,
        // because a database that is briefly away must not make an orchestrator
        // restart a perfectly healthy process. Readiness answers "can it serve
        // traffic", and that does need the database.
        services
            .AddHealthChecks()
            .AddDbContextCheck<AppDbContext>(
                name: "database",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["ready"]);

        return services;
    }

    public static WebApplication MapApiHealthChecks(this WebApplication app)
    {
        app.MapHealthChecks("/health/live", new()
        {
            Predicate = _ => false
        });

        app.MapHealthChecks("/health/ready", new()
        {
            Predicate = check => check.Tags.Contains("ready")
        });

        // Kept as an alias of readiness: the compose healthcheck, the smoke
        // scripts and Render's health path all point at it.
        app.MapHealthChecks("/health", new()
        {
            Predicate = check => check.Tags.Contains("ready")
        });

        return app;
    }

    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app) =>
        app.UseMiddleware<CorrelationIdMiddleware>();
}