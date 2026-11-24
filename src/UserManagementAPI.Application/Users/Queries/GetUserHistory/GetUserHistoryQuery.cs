using UserManagementAPI.Application.Abstractions;
using UserManagementAPI.Application.Users.Dtos;

namespace UserManagementAPI.Application.Users.Queries.GetUserHistory;

/// <summary>
/// A user's changes, newest first, at most <see cref="Limit"/>. Handled in
/// Infrastructure, where the audit log lives (ADR 0016, ADR 0025); an unknown
/// id is a User.NotFound failure.
/// </summary>
public sealed record GetUserHistoryQuery(Guid UserId) : IQuery<IReadOnlyList<UserHistoryEntryDto>>
{
    public const int Limit = 100;
}
