using UserManagementAPI.Application.Abstractions;
using UserManagementAPI.Domain.Common;

namespace UserManagementAPI.Application.Behaviors;

/// <summary>
/// Wraps a command handler in one transaction. Commands only — the constraint
/// on IBaseCommand means the container never resolves this for a query, so
/// reads are not wrapped in a transaction they would never use.
///
/// The transaction itself is opened by the unit of work, because with a retrying
/// execution strategy (EnableRetryOnFailure, M2 PR8) a bare BeginTransaction
/// throws at runtime; it has to be opened inside that strategy, and only the
/// persistence layer knows which strategy is in play.
/// </summary>
public sealed class TransactionBehavior<TRequest, TResponse>(IUnitOfWork unitOfWork)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IBaseCommand
    where TResponse : Result
{
    public Task<TResponse> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(() => next(), cancellationToken);
}