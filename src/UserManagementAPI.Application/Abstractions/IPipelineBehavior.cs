using UserManagementAPI.Domain.Common;

namespace UserManagementAPI.Application.Abstractions;

/// <summary>The rest of the pipeline, ending in the handler.</summary>
public delegate Task<TResponse> RequestHandlerDelegate<TResponse>()
    where TResponse : Result;

/// <summary>
/// A cross-cutting step that runs around a handler: logging, validation, a
/// transaction. Behaviors are registered as open generics and resolved per
/// request type; the dispatcher runs them in registration order, the first
/// registered being the outermost.
///
/// A behavior is not middleware. Middleware sees an HttpContext and knows
/// nothing about commands; a behavior sees a command and knows nothing about
/// HTTP.
/// </summary>
public interface IPipelineBehavior<in TRequest, TResponse>
    where TResponse : Result
{
    Task<TResponse> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken);
}