using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using UserManagementAPI.Application.Abstractions;
using UserManagementAPI.Domain.Roles;
using UserManagementAPI.Domain.Users;
using UserManagementAPI.Infrastructure.Events;
using UserManagementAPI.Infrastructure.Persistence;
using UserManagementAPI.Infrastructure.Persistence.Interceptors;
using UserManagementAPI.Infrastructure.Persistence.Repositories;

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
            .AddInterceptors(serviceProvider.GetRequiredService<DomainEventDispatchInterceptor>()));

        services.AddScoped<IUnitOfWork>(serviceProvider => serviceProvider.GetRequiredService<AppDbContext>());
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();

        return services;
    }
}