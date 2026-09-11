using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using UserManagementAPI.Application.Abstractions;
using UserManagementAPI.Domain.Auth;
using UserManagementAPI.Domain.Roles;
using UserManagementAPI.Domain.Users;
using UserManagementAPI.Infrastructure.Auditing;
using UserManagementAPI.Infrastructure.Events;
using UserManagementAPI.Infrastructure.Identity;
using UserManagementAPI.Infrastructure.Outbox;
using UserManagementAPI.Infrastructure.Persistence;
using UserManagementAPI.Infrastructure.Persistence.Interceptors;
using UserManagementAPI.Infrastructure.Persistence.Repositories;
using UserManagementAPI.Infrastructure.Persistence.Seed;

namespace UserManagementAPI.Infrastructure;

/// <summary>
/// The only place Api touches Infrastructure. Called once from Program.cs; no
/// controller names a type from this assembly.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:Default is not configured. Set it in user secrets locally " +
                "or as ConnectionStrings__Default in a deployed environment.");
        }

        services.AddScoped<IDomainEventPublisher, DomainEventPublisher>();
        services.AddScoped<DomainEventDispatchInterceptor>();

        // The Api registers its HTTP-aware ICurrentUser before calling this;
        // everywhere else — seeder, outbox processor, tests — the actor is the
        // system.
        services.TryAddScoped<ICurrentUser, SystemCurrentUser>();
        services.AddScoped<AuditInterceptor>();

        services.AddDbContext<AppDbContext>((serviceProvider, options) => options
            .UseNpgsql(connectionString, npgsql =>
            {
                // Neon autosuspends idle compute, so the first request after a
                // pause can fail outright rather than merely being slow.
                npgsql.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorCodesToAdd: null);

                npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
            })
            .UseSnakeCaseNamingConvention()
            // Order matters: events dispatch first, then the audit sees what the
            // handlers changed.
            .AddInterceptors(
                serviceProvider.GetRequiredService<DomainEventDispatchInterceptor>(),
                serviceProvider.GetRequiredService<AuditInterceptor>()));

        services.AddScoped<IUnitOfWork>(serviceProvider => serviceProvider.GetRequiredService<AppDbContext>());
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

        services.Configure<Argon2Options>(configuration.GetSection(Argon2Options.SectionName));
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<SeedOptions>(configuration.GetSection(SeedOptions.SectionName));

        services.AddSingleton<IPasswordHasher, Argon2PasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddScoped<IPermissionLookup, PermissionLookup>();

        services.Configure<OutboxOptions>(configuration.GetSection(OutboxOptions.SectionName));
        services.AddScoped<IOutboxWriter, OutboxWriter>();
        services.AddSingleton<IOutboxPublisher, LoggingOutboxPublisher>();
        services.AddScoped<OutboxBatchProcessor>();
        services.AddHostedService<OutboxProcessor>();

        AddQueryHandlers(services);

        return services;
    }

    /// <summary>
    /// Query handlers live in this assembly, next to the DbContext they project
    /// from (ADR 0016); AddApplication scans only the Application assembly, so
    /// they are registered here the same way — by scanning, not by list.
    /// </summary>
    private static void AddQueryHandlers(IServiceCollection services)
    {
        var registrations = typeof(DependencyInjection).Assembly
            .GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false })
            .SelectMany(type => type
                .GetInterfaces()
                .Where(implemented =>
                    implemented.IsGenericType && implemented.GetGenericTypeDefinition() == typeof(IQueryHandler<,>))
                .Select(implemented => (Service: implemented, Implementation: type)));

        foreach (var (service, implementation) in registrations)
        {
            services.AddScoped(service, implementation);
        }
    }
}