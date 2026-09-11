using System.Collections.Concurrent;

using Microsoft.Extensions.DependencyInjection;

using UserManagementAPI.Application.Abstractions;
using UserManagementAPI.Domain.Common;

namespace UserManagementAPI.Application.Dispatching;

/// <summary>
/// Resolves the handler for a request by its closed generic type and runs it
/// behind the registered behaviors. Hand-written rather than a mediator library
/// (ADR 0003): the whole thing is one class, it has no licence, and there is
/// nothing in it that a reader cannot follow in a minute.
///
/// A request arrives typed as its interface (ICommand, IQuery&lt;T&gt;), so the
/// concrete type is only known at runtime. A closed wrapper is created once per
/// request type and cached; inside the wrapper everything is statically typed.
/// </summary>
public sealed class Dispatcher(IServiceProvider serviceProvider) : IDispatcher
{
    private static readonly ConcurrentDictionary<Type, RequestWrapper> Wrappers = new();

    public Task<Result> SendAsync(ICommand command, CancellationToken cancellationToken = default) =>
        (Task<Result>)Wrapper(command, typeof(CommandWrapper<>), []).HandleAsync(command, serviceProvider, cancellationToken);

    public Task<Result<TResponse>> SendAsync<TResponse>(
        ICommand<TResponse> command,
        CancellationToken cancellationToken = default) =>
        (Task<Result<TResponse>>)Wrapper(command, typeof(CommandWrapper<,>), [typeof(TResponse)])
            .HandleAsync(command, serviceProvider, cancellationToken);

    public Task<Result<TResponse>> QueryAsync<TResponse>(
        IQuery<TResponse> query,
        CancellationToken cancellationToken = default) =>
        (Task<Result<TResponse>>)Wrapper(query, typeof(QueryWrapper<,>), [typeof(TResponse)])
            .HandleAsync(query, serviceProvider, cancellationToken);

    private static RequestWrapper Wrapper(object request, Type openWrapperType, Type[] responseTypes) =>
        Wrappers.GetOrAdd(request.GetType(), requestType =>
            (RequestWrapper)Activator.CreateInstance(
                openWrapperType.MakeGenericType([requestType, .. responseTypes]))!);

    private abstract class RequestWrapper
    {
        public abstract Task HandleAsync(object request, IServiceProvider services, CancellationToken cancellationToken);

        /// <summary>
        /// Builds the chain from the inside out. Reversing the registration order
        /// before wrapping means the first registered behavior ends up outermost.
        /// </summary>
        protected static Task<TResponse> RunPipeline<TRequest, TResponse>(
            TRequest request,
            IServiceProvider services,
            RequestHandlerDelegate<TResponse> handler,
            CancellationToken cancellationToken)
            where TResponse : Result
        {
            var pipeline = handler;

            foreach (var behavior in services.GetServices<IPipelineBehavior<TRequest, TResponse>>().Reverse())
            {
                var next = pipeline;
                pipeline = () => behavior.HandleAsync(request, next, cancellationToken);
            }

            return pipeline();
        }

        protected static THandler ResolveHandler<THandler>(IServiceProvider services, Type requestType)
            where THandler : class =>
            services.GetService<THandler>()
            ?? throw new InvalidOperationException(
                $"No handler is registered for {requestType.Name}. " +
                "Handlers are registered by AddApplication() scanning the Application assembly.");
    }

    private sealed class CommandWrapper<TCommand> : RequestWrapper
        where TCommand : ICommand
    {
        public override Task HandleAsync(object request, IServiceProvider services, CancellationToken cancellationToken)
        {
            var command = (TCommand)request;
            var handler = ResolveHandler<ICommandHandler<TCommand>>(services, typeof(TCommand));

            return RunPipeline<TCommand, Result>(
                command, services, () => handler.HandleAsync(command, cancellationToken), cancellationToken);
        }
    }

    private sealed class CommandWrapper<TCommand, TResponse> : RequestWrapper
        where TCommand : ICommand<TResponse>
    {
        public override Task HandleAsync(object request, IServiceProvider services, CancellationToken cancellationToken)
        {
            var command = (TCommand)request;
            var handler = ResolveHandler<ICommandHandler<TCommand, TResponse>>(services, typeof(TCommand));

            return RunPipeline<TCommand, Result<TResponse>>(
                command, services, () => handler.HandleAsync(command, cancellationToken), cancellationToken);
        }
    }

    private sealed class QueryWrapper<TQuery, TResponse> : RequestWrapper
        where TQuery : IQuery<TResponse>
    {
        public override Task HandleAsync(object request, IServiceProvider services, CancellationToken cancellationToken)
        {
            var query = (TQuery)request;
            var handler = ResolveHandler<IQueryHandler<TQuery, TResponse>>(services, typeof(TQuery));

            return RunPipeline<TQuery, Result<TResponse>>(
                query, services, () => handler.HandleAsync(query, cancellationToken), cancellationToken);
        }
    }
}