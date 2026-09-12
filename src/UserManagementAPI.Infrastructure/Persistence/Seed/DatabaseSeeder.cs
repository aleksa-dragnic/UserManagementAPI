using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using UserManagementAPI.Application.Abstractions;
using UserManagementAPI.Domain.Roles;
using UserManagementAPI.Domain.Users;

namespace UserManagementAPI.Infrastructure.Persistence.Seed;

/// <summary>
/// Puts the permission codes, the two shipped roles and the bootstrap
/// administrator in place. Idempotent by design: it checks before it inserts,
/// so it can run on every startup in development without accumulating
/// duplicates or failing on the second boot.
///
/// The administrator was deferred from M2 PR9 to here, where a real password
/// hasher exists. Its password comes from configuration and is never logged.
/// </summary>
public sealed class DatabaseSeeder(
    AppDbContext context,
    IPasswordHasher passwordHasher,
    IOptions<SeedOptions> seedOptions,
    ILogger<DatabaseSeeder> logger)
{
    private readonly SeedOptions _seed = seedOptions.Value;

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var seededPermissions = await SeedPermissionsAsync(cancellationToken);
        var (administratorRoleId, memberRoleId) = await SeedRolesAsync(seededPermissions, cancellationToken);
        await SeedAdministratorAsync(administratorRoleId, cancellationToken);
        await SeedDemoUserAsync(memberRoleId, cancellationToken);
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

    /// <summary>Returns the role ids the seeded accounts need.</summary>
    private async Task<(Guid AdministratorRoleId, Guid MemberRoleId)> SeedRolesAsync(
        Dictionary<string, Guid> permissionIds,
        CancellationToken cancellationToken)
    {
        var administrator = await SeedRoleAsync(RoleNames.Administrator, PermissionCodes.All, permissionIds, cancellationToken);
        var member = await SeedRoleAsync(RoleNames.Member, PermissionCodes.ReadOnly, permissionIds, cancellationToken);

        await context.SaveChangesAsync(cancellationToken);

        return (administrator.Id, member.Id);
    }

    private async Task<Role> SeedRoleAsync(
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

        return role;
    }

    private async Task SeedAdministratorAsync(Guid administratorRoleId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_seed.AdministratorPassword))
        {
            logger.LogWarning(
                "Seed:AdministratorPassword is not configured; the bootstrap administrator was not seeded.");

            return;
        }

        var email = Email.Create(_seed.AdministratorEmail).Value;

        if (await context.Users.AnyAsync(user => user.Email.Value == email.Value, cancellationToken))
        {
            return;
        }

        var passwordHash = PasswordHash.Create(passwordHasher.Hash(_seed.AdministratorPassword)).Value;
        var name = PersonName.Create("System", "Administrator").Value;

        var administrator = User.Register(email, name, passwordHash).Value;
        administrator.VerifyEmail();
        administrator.AssignRole(administratorRoleId);

        context.Users.Add(administrator);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Seeding bootstrap administrator {AdministratorEmail}.", email.Value);
    }

    /// <summary>
    /// The read-only account the public demo advertises. Seeded only where a
    /// password is configured, which is the deployed instance and nowhere else —
    /// a demo account with a published password has no business existing in a
    /// developer's database.
    /// </summary>
    private async Task SeedDemoUserAsync(Guid memberRoleId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_seed.DemoPassword))
        {
            return;
        }

        var email = Email.Create(_seed.DemoEmail).Value;

        if (await context.Users.AnyAsync(user => user.Email.Value == email.Value, cancellationToken))
        {
            return;
        }

        var passwordHash = PasswordHash.Create(passwordHasher.Hash(_seed.DemoPassword)).Value;
        var demo = User.Register(email, PersonName.Create("Demo", "Reader").Value, passwordHash).Value;

        demo.VerifyEmail();
        demo.AssignRole(memberRoleId);

        context.Users.Add(demo);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Seeding read-only demo account {DemoEmail}.", email.Value);
    }
}