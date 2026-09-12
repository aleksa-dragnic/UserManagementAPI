using Riok.Mapperly.Abstractions;

using UserManagementAPI.Api.Contracts.V1;
using UserManagementAPI.Application.Users.Commands.AssignRole;
using UserManagementAPI.Application.Users.Commands.RegisterUser;
using UserManagementAPI.Application.Users.Commands.UpdateUser;
using UserManagementAPI.Application.Users.Queries.GetUsers;

namespace UserManagementAPI.Api.Mapping;

/// <summary>
/// Request contract to command or query, generated at compile time by Mapperly
/// (ADR 0014). The extra Guid parameters are the route values; Mapperly matches them
/// to the command's constructor parameters by name.
/// </summary>
[Mapper]
public static partial class UserRequestMapper
{
    public static partial RegisterUserCommand ToCommand(this RegisterUserRequest request);

    public static partial UpdateUserCommand ToCommand(this UpdateUserRequest request, Guid userId);

    public static partial AssignRoleCommand ToCommand(this AssignRoleRequest request, Guid userId);

    public static partial GetUsersQuery ToQuery(this UserQueryParameters parameters);
}