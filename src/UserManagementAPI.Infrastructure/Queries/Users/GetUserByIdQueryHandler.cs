using Microsoft.EntityFrameworkCore;

using UserManagementAPI.Application.Abstractions;
using UserManagementAPI.Application.Users.Dtos;
using UserManagementAPI.Application.Users.Queries.GetUserById;
using UserManagementAPI.Domain.Common;
using UserManagementAPI.Domain.Users;
using UserManagementAPI.Infrastructure.Persistence;

namespace UserManagementAPI.Infrastructure.Queries.Users;

/// <summary>
/// One user and the names of the roles they hold. The User aggregate only knows
/// role ids, so the names come from a join the database does in the same query —
/// the read side is allowed to cross an aggregate boundary the write side never
/// crosses, because it changes nothing.
/// </summary>
internal sealed class GetUserByIdQueryHandler(AppDbContext context)
    : IQueryHandler<GetUserByIdQuery, UserDetailsDto>
{
    public async Task<Result<UserDetailsDto>> HandleAsync(
        GetUserByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        var user = await context.Users
            .AsNoTracking()
            .Where(candidate => candidate.Id == query.UserId)
            .Select(candidate => new UserDetailsDto(
                candidate.Id,
                candidate.Email.Value,
                candidate.Name.First,
                candidate.Name.Last,
                candidate.Status.Name,
                (from assignment in candidate.Roles
                 join role in context.Roles on assignment.RoleId equals role.Id
                 orderby role.Name
                 select new UserRoleDto(role.Id, role.Name, assignment.AssignedAtUtc))
                .ToList(),
                candidate.CreatedAtUtc,
                candidate.UpdatedAtUtc))
            .SingleOrDefaultAsync(cancellationToken);

        return user is null
            ? Result.Failure<UserDetailsDto>(User.NotFound)
            : Result.Success(user);
    }
}