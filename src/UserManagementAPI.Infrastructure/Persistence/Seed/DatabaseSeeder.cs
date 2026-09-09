using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using UserManagementAPI.Domain.Roles;

namespace UserManagementAPI.Infrastructure.Persistence.Seed;

/// <summary>
/// Puts the permission codes and the two shipped roles in place. Idempotent by
/// design: it checks before it inserts, so it can run on every startup in
/// development without accumulating duplicates or failing on the second boot.
/// </summary>
public sealed class DatabaseSeeder(AppDbContext context, ILogger<DatabaseSeeder> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var seededPermissions = await SeedPermissionsAsync(cancellationToken);
        await SeedRolesAsync(seededPermissions, cancellationToken);
    }

    private async Task<Dictionary<string, Guid>> SeedPermissionsAsync(CancellationToken cancellationToken)
    {
        var existing = await context.Permissions
            .ToDictionaryAsync(permission => permission.Code, permission => permission.Id, cancellationToken);

        foreach (var code in PermissionCodes.All.Where(code => !existing.ContainsKey(code)))
        {
            var permission = Permission.Create(code).Value;

            context.Permissions.Add(permission);
            existing[code] = permission.Id;

            logger.LogInformation("Seeding permission {PermissionCode}.", code);
        }

        await context.SaveChangesAsync(cancellationToken);

        return existing;
    }

    private async Task SeedRolesAsync(
        Dictionary<string, Guid> permissionIds,
        CancellationToken cancellationToken)
    {
        await SeedRoleAsync(RoleNames.Administrator, PermissionCodes.All, permissionIds, cancellationToken);
        await SeedRoleAsync(RoleNames.Member, PermissionCodes.ReadOnly, permissionIds, cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedRoleAsync(
        string name,
        IReadOnlyCollection<string> codes,
        Dictionary<string, Guid> permissionIds,
        CancellationToken cancellationToken)
    {
        var role = await context.Roles
            .Include(candidate => candidate.Permissions)
            .FirstOrDefaultAsync(candidate => candidate.Name == name, cancellationToken);

        if (role is null)
        {
            role = Role.Create(name).Value;
            context.Roles.Add(role);

            logger.LogInformation("Seeding role {RoleName}.", name);
        }

        // Re-checked on every run rather than only at creation: a code added to
        // a later release has to reach a role that already exists.
        foreach (var code in codes)
        {
            role.AddPermission(permissionIds[code]);
        }
    }
}