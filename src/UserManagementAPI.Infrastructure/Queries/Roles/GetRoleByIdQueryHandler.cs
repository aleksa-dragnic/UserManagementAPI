using Microsoft.EntityFrameworkCore;

using UserManagementAPI.Application.Abstractions;
using UserManagementAPI.Application.Roles.Dtos;
using UserManagementAPI.Application.Roles.Queries.GetRoleById;
using UserManagementAPI.Domain.Common;
using UserManagementAPI.Domain.Roles;
using UserManagementAPI.Infrastructure.Persistence;

namespace UserManagementAPI.Infrastructure.Queries.Roles;

internal sealed class GetRoleByIdQueryHandler(AppDbContext context)
    : IQueryHandler<GetRoleByIdQuery, RoleDto>
{
    public async Task<Result<RoleDto>> HandleAsync(
        GetRoleByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        var role = await context.Roles
            .AsNoTracking()
            .Where(candidate => candidate.Id == query.RoleId)
            .Select(RoleReadModels.ToDto(context))
            .SingleOrDefaultAsync(cancellationToken);

        return role is null
            ? Result.Failure<RoleDto>(Role.NotFound)
            : Result.Success(role);
    }
}