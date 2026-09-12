using UserManagementAPI.Application.Abstractions;
using UserManagementAPI.Application.Common;
using UserManagementAPI.Application.Users.Dtos;

namespace UserManagementAPI.Application.Users.Queries.GetUsers;

/// <summary>
/// A page of users, filtered, searched and sorted by the parameters it
/// inherits. Handled in Infrastructure (ADR 0016).
/// </summary>
public sealed record GetUsersQuery : UserParameters, IQuery<PagedList<UserDto>>;