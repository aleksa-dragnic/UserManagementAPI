using UserManagementAPI.Domain.Common;

namespace UserManagementAPI.Application.Abstractions;

/// <summary>
/// Commits the work accumulated on the current aggregate graph. It deliberately
/// exposes no repositories: a handler asks a repository for an aggregate and
/// asks this for a commit, and neither knows about the other.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs an operation inside one database transaction. The transaction is
    /// committed when the operation returns a successful result and rolled back
    /// when it returns a failure or throws — a failed Result is an expected
    /// outcome, and it must not leave half a write behind any more than an
    /// exception would.
    ///
    /// Defined here rather than in a behavior because starting a transaction is
    /// a persistence concern: with a retrying execution strategy the transaction
    /// must be opened inside that strategy, and only the implementation knows it.
    /// </summary>
    Task<TResponse> ExecuteInTransactionAsync<TResponse>(
        Func<Task<TResponse>> operation,
        CancellationToken cancellationToken = default)
        where TResponse : Result;
}