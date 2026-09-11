using UserManagementAPI.Application.Abstractions;
using UserManagementAPI.Application.Roles.Dtos;

namespace UserManagementAPI.Application.Roles.Queries.GetRoles;

/// <summary>
/// Every role, ordered by name. A client needs a role's id to assign it, and
/// this is where it gets one. Handled in Infrastructure (ADR 0016).
/// </summary>
public sealed record GetRolesQuery : IQuery<IReadOnlyList<RoleDto>>;