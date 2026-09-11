using Microsoft.EntityFrameworkCore;

using UserManagementAPI.Application.Abstractions;
using UserManagementAPI.Application.Users.Dtos;
using UserManagementAPI.Application.Users.Queries.GetUsers;
using UserManagementAPI.Domain.Common;
using UserManagementAPI.Infrastructure.Persistence;

namespace UserManagementAPI.Infrastructure.Queries.Users;

/// <summary>
/// Reads straight from the DbContext: no aggregate, no repository, no change
/// tracking. The projection runs in the database (ADR 0016).
/// </summary>
internal sealed class GetUsersQueryHandler(AppDbContext context)
    : IQueryHandler<GetUsersQuery, IReadOnlyList<UserDto>>
{
    public async Task<Result<IReadOnlyList<UserDto>>> HandleAsync(
        GetUsersQuery query,
        CancellationToken cancellationToken = default)
    {
        var users = await context.Users
            .AsNoTracking()
            .OrderBy(user => user.Email.Value)
            .Select(UserReadModels.ToDto)
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<UserDto>>(users);
    }
}