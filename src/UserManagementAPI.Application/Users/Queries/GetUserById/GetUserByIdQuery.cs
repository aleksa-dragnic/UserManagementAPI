using UserManagementAPI.Application.Abstractions;
using UserManagementAPI.Application.Users.Dtos;

namespace UserManagementAPI.Application.Users.Queries.GetUserById;

/// <summary>
/// One user with their roles. Handled in Infrastructure, next to the DbContext
/// it projects from (ADR 0016); an unknown id is a User.NotFound failure.
/// </summary>
public sealed record GetUserByIdQuery(Guid UserId) : IQuery<UserDetailsDto>;