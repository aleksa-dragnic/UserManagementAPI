using FluentValidation;

using Microsoft.Extensions.DependencyInjection;

using UserManagementAPI.Application.Abstractions;
using UserManagementAPI.Application.Behaviors;
using UserManagementAPI.Application.Dispatching;

namespace UserManagementAPI.Application;

/// <summary>
/// Registers the dispatcher, the behaviors, the validators and every handler in
/// this assembly. Called once from Program.cs. Handlers are found by scanning
/// rather than listed by hand, so adding a use case means adding a folder, not
/// editing a registration list that is easy to forget.
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

        // Registration order is execution order: first registered is outermost.
        // Logging sees everything, including validation failures; validation
        // rejects before a transaction is opened; the transaction wraps only the
        // handler. TransactionBehavior is constrained to commands and the
        // container skips it for queries.
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));

        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);

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