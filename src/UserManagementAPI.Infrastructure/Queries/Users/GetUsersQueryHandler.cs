using Microsoft.EntityFrameworkCore;

using UserManagementAPI.Application.Abstractions;
using UserManagementAPI.Application.Common;
using UserManagementAPI.Application.Users.Dtos;
using UserManagementAPI.Application.Users.Queries.GetUsers;
using UserManagementAPI.Domain.Common;
using UserManagementAPI.Infrastructure.Persistence;

namespace UserManagementAPI.Infrastructure.Queries.Users;

/// <summary>
/// A page of users in two statements: the count over the filtered set, then the
/// sorted page with the projection as the last operator. Filter, search and sort
/// are the Application's LINQ (UserQueryExtensions); this handler only composes
/// them against the DbContext and runs them (ADR 0016).
/// </summary>
internal sealed class GetUsersQueryHandler(AppDbContext context)
    : IQueryHandler<GetUsersQuery, PagedList<UserDto>>
{
    public async Task<Result<PagedList<UserDto>>> HandleAsync(
        GetUsersQuery query,
        CancellationToken cancellationToken = default)
    {
        var filtered = context.Users
            .AsNoTracking()
            .Filter(query.Status)
            .Search(query.SearchTerm);

        var totalCount = await filtered.CountAsync(cancellationToken);

        // Computed as a long: a page number near int.MaxValue must produce an
        // empty page, not an overflowed negative OFFSET.
        var skip = (long)(query.PageNumber - 1) * query.PageSize;

        IReadOnlyList<UserDto> items;

        if (skip >= totalCount)
        {
            items = [];
        }
        else
        {
            items = await filtered
                .Sort(query.OrderBy)
                .Skip((int)skip)
                .Take(query.PageSize)
                .Select(UserReadModels.ToDto)
                .ToListAsync(cancellationToken);
        }

        var metaData = MetaData.Create(query.PageNumber, query.PageSize, totalCount);

        return Result.Success(new PagedList<UserDto>(items, metaData));
    }
}