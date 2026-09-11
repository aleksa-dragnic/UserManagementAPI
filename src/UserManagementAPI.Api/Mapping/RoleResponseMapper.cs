using Riok.Mapperly.Abstractions;

using UserManagementAPI.Api.Contracts.V1;
using UserManagementAPI.Application.Roles.Dtos;

namespace UserManagementAPI.Api.Mapping;

[Mapper]
public static partial class RoleResponseMapper
{
    public static partial RoleResponse ToResponse(this RoleDto role);
}