using UserManagementAPI.Domain.Common;

namespace UserManagementAPI.Application.Abstractions;

/// <summary>
/// Handles a command that returns no value. A handler orchestrates — it loads
/// an aggregate, calls a method on it, and commits. A business rule expressed as
/// an "if" in a handler belongs in the aggregate.
/// </summary>
public interface ICommandHandler<in TCommand>
    where TCommand : ICommand
{
    Task<Result> HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}

/// <summary>Handles a command that returns a value on success.</summary>
public interface ICommandHandler<in TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    Task<Result<TResponse>> HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}