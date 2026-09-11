using UserManagementAPI.Application.Abstractions;
using UserManagementAPI.Domain.Common;

namespace UserManagementAPI.Application.UnitTests.Dispatching;

/// <summary>
/// Minimal requests, handlers and behaviors for exercising the dispatcher. They
/// live in the test assembly, which AddApplication does not scan, so every test
/// registers exactly what it needs.
/// </summary>
internal sealed record PingCommand(string Message) : ICommand<string>;

internal sealed class PingCommandHandler : ICommandHandler<PingCommand, string>
{
    public Task<Result<string>> HandleAsync(PingCommand command, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Success($"pong: {command.Message}"));
}

internal sealed record TouchCommand : ICommand;

internal sealed class TouchCommandHandler : ICommandHandler<TouchCommand>
{
    public int Calls { get; private set; }

    public Task<Result> HandleAsync(TouchCommand command, CancellationToken cancellationToken = default)
    {
        Calls++;
        return Task.FromResult(Result.Success());
    }
}

internal sealed record CountQuery : IQuery<int>;

internal sealed class CountQueryHandler : IQueryHandler<CountQuery, int>
{
    public Task<Result<int>> HandleAsync(CountQuery query, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Success(42));
}

internal sealed record OrphanCommand : ICommand;

/// <summary>Appends its name before and after the rest of the pipeline, so ordering is observable.</summary>
internal sealed class RecordingBehavior<TRequest, TResponse>(string name, List<string> log)
    : IPipelineBehavior<TRequest, TResponse>
    where TResponse : Result
{
    public async Task<TResponse> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        log.Add($"{name}:before");
        var response = await next();
        log.Add($"{name}:after");
        return response;
    }
}