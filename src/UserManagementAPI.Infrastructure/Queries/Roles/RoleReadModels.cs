using System.Linq.Expressions;

using UserManagementAPI.Application.Roles.Dtos;
using UserManagementAPI.Domain.Roles;
using UserManagementAPI.Infrastructure.Persistence;

namespace UserManagementAPI.Infrastructure.Queries.Roles;

/// <summary>
/// Role to RoleDto, with the permission codes resolved by a join: the Role
/// aggregate holds permission ids only. Built per context because the join
/// reads the Permissions set of the context running the query.
/// </summary>
internal static class RoleReadModels
{
    public static Expression<Func<Role, RoleDto>> ToDto(AppDbContext context) => role => new RoleDto(
        role.Id,
        role.Name,
        (from held in role.Permissions
         join permission in context.Permissions on held.PermissionId equals permission.Id
         orderby permission.Code
         select permission.Code)
        .ToList());
}