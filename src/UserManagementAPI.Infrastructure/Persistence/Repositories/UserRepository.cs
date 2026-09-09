using Microsoft.EntityFrameworkCore;

using UserManagementAPI.Domain.Users;

namespace UserManagementAPI.Infrastructure.Persistence.Repositories;

/// <summary>
/// Write-side access to the User aggregate. Roles are included because
/// AssignRole and RemoveRole reason over the existing set; nothing else is.
/// </summary>
public sealed class UserRepository(AppDbContext context) : IUserRepository
{
    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Users
            .Include(user => user.Roles)
            .FirstOrDefaultAsync(user => user.Id == id, cancellationToken);

    public Task<User?> GetByEmailAsync(Email email, CancellationToken cancellationToken = default) =>
        context.Users
            .Include(user => user.Roles)
            // Compared by column, not by owned type: EF Core does not translate
            // equality over an owned instance.
            .FirstOrDefaultAsync(user => user.Email.Value == email.Value, cancellationToken);

    public Task<bool> ExistsByEmailAsync(Email email, CancellationToken cancellationToken = default) =>
        context.Users.AnyAsync(user => user.Email.Value == email.Value, cancellationToken);

    public void Add(User user) => context.Users.Add(user);

    public void Update(User user) => context.Users.Update(user);
}