using Microsoft.EntityFrameworkCore;

using UserManagementAPI.Domain.Roles;

namespace UserManagementAPI.Infrastructure.Persistence.Repositories;

public sealed class RoleRepository(AppDbContext context) : IRoleRepository
{
    public Task<Role?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Roles
            .Include(role => role.Permissions)
            .FirstOrDefaultAsync(role => role.Id == id, cancellationToken);

    public Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken = default) =>
        context.Roles
            .Include(role => role.Permissions)
            .FirstOrDefaultAsync(role => role.Name == name, cancellationToken);

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Roles.AnyAsync(role => role.Id == id, cancellationToken);
}