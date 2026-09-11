using Microsoft.EntityFrameworkCore;

using UserManagementAPI.Application.Abstractions;
using UserManagementAPI.Domain.Roles;
using UserManagementAPI.Domain.Users;

namespace UserManagementAPI.Infrastructure.Persistence;

/// <summary>
/// user_roles → role_permissions → permissions in one query, projected to the
/// codes. No aggregate is loaded: this is a read, and the read side never goes
/// through the domain model.
/// </summary>
internal sealed class PermissionLookup(AppDbContext context) : IPermissionLookup
{
    public async Task<IReadOnlyCollection<string>> GetPermissionCodesAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var codes = await (
            from userRole in context.Set<UserRole>()
            where userRole.UserId == userId
            join rolePermission in context.Set<RolePermission>() on userRole.RoleId equals rolePermission.RoleId
            join permission in context.Permissions on rolePermission.PermissionId equals permission.Id
            select permission.Code)
            .Distinct()
            .ToListAsync(cancellationToken);

        return codes;
    }
}