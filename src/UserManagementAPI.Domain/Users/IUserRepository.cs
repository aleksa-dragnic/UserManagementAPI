namespace UserManagementAPI.Domain.Users;

/// <summary>
/// Write-side access to the User aggregate. No IQueryable and no query methods:
/// reads project straight to DTOs in M5 and never load an aggregate to do it.
/// </summary>
public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<User?> GetByEmailAsync(Email email, CancellationToken cancellationToken = default);

    Task<bool> ExistsByEmailAsync(Email email, CancellationToken cancellationToken = default);

    void Add(User user);

    void Update(User user);
}