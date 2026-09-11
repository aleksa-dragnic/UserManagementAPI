using UserManagementAPI.Application.Abstractions;
using UserManagementAPI.Application.Roles.Dtos;

namespace UserManagementAPI.Application.Roles.Queries.GetRoleById;

/// <summary>One role with its permission codes; an unknown id is a Role.NotFound failure.</summary>
public sealed record GetRoleByIdQuery(Guid RoleId) : IQuery<RoleDto>;