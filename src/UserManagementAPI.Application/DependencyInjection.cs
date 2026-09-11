using Microsoft.Extensions.DependencyInjection;

using UserManagementAPI.Application.Abstractions;
using UserManagementAPI.Application.Dispatching;

namespace UserManagementAPI.Application;

/// <summary>
/// Registers the dispatcher and every handler in this assembly. Called once
/// from Program.cs. Handlers are found by scanning rather than listed by hand,
/// so adding a use case means adding a folder, not editing a registration list
/// that is easy to forget.
/// </summary>
public static class DependencyInjection
{
    private static readonly Type[] HandlerInterfaces =
    [
        typeof(ICommandHandler<>),
        typeof(ICommandHandler<,>),
        typeof(IQueryHandler<,>)
    ];

    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IDispatcher, Dispatcher>();

        var handlerRegistrations = typeof(DependencyInjection).Assembly
            .GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false })
            .SelectMany(type => type
                .GetInterfaces()
                .Where(implemented =>
                    implemented.IsGenericType && HandlerInterfaces.Contains(implemented.GetGenericTypeDefinition()))
                .Select(implemented => (Service: implemented, Implementation: type)));

        foreach (var (service, implementation) in handlerRegistrations)
        {
            services.AddScoped(service, implementation);
        }

        return services;
    }
}