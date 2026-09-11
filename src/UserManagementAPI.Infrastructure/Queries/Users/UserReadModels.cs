using System.Linq.Expressions;

using UserManagementAPI.Application.Users.Dtos;
using UserManagementAPI.Domain.Users;

namespace UserManagementAPI.Infrastructure.Queries.Users;

/// <summary>
/// The one projection from the users table to UserDto. It is an expression, so
/// EF Core turns it into the SELECT list: the database returns the columns the
/// DTO needs and nothing else — the password hash never leaves the table on a
/// read.
///
/// Status is the one member finished after the query: the column holds the
/// enumeration's id, EF materialises the UserStatus through the value converter
/// and the name is read from it. That is why the projection is always the last
/// operator of a query.
/// </summary>
internal static class UserReadModels
{
    public static readonly Expression<Func<User, UserDto>> ToDto = user => new UserDto(
        user.Id,
        user.Email.Value,
        user.Name.First,
        user.Name.Last,
        user.Status.Name);
}