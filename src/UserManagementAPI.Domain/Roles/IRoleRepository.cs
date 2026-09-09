namespace UserManagementAPI.Domain.Roles;

/// <summary>
/// Write-side access to the Role aggregate. Same rule as IUserRepository: no
/// IQueryable, no query methods.
/// </summary>
public interface IRoleRepository
{
    Task<Role?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
}