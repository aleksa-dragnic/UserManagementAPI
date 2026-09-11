using Microsoft.EntityFrameworkCore;

using UserManagementAPI.Application.Abstractions;
using UserManagementAPI.Application.Roles.Dtos;
using UserManagementAPI.Application.Roles.Queries.GetRoles;
using UserManagementAPI.Domain.Common;
using UserManagementAPI.Infrastructure.Persistence;

namespace UserManagementAPI.Infrastructure.Queries.Roles;

internal sealed class GetRolesQueryHandler(AppDbContext context)
    : IQueryHandler<GetRolesQuery, IReadOnlyList<RoleDto>>
{
    public async Task<Result<IReadOnlyList<RoleDto>>> HandleAsync(
        GetRolesQuery query,
        CancellationToken cancellationToken = default)
    {
        var roles = await context.Roles
            .AsNoTracking()
            .OrderBy(role => role.Name)
            .Select(RoleReadModels.ToDto(context))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<RoleDto>>(roles);
    }
}