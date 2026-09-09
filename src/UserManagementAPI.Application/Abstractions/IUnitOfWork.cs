namespace UserManagementAPI.Application.Abstractions;

/// <summary>
/// Commits the work accumulated on the current aggregate graph. It deliberately
/// exposes no repositories: a handler asks a repository for an aggregate and
/// asks this for a commit, and neither knows about the other.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}