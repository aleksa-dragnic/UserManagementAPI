using UserManagementAPI.Application.Abstractions;
using UserManagementAPI.Application.Users.Dtos;

namespace UserManagementAPI.Application.Users.Queries.GetUsers;

/// <summary>
/// Every user, ordered by email. A flat list for now; paging, filtering,
/// searching and sorting arrive in M5 PR19. Handled in Infrastructure (ADR 0016).
/// </summary>
public sealed record GetUsersQuery : IQuery<IReadOnlyList<UserDto>>;