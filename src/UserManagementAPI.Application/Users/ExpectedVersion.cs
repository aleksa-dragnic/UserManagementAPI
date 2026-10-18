using UserManagementAPI.Domain.Users;

namespace UserManagementAPI.Application.Users;

/// <summary>
/// The check every user write makes when the client sent the version it read
/// (ADR 0021). No version means no check: clients that predate it keep working.
/// </summary>
internal static class ExpectedVersion
{
    public static bool IsCurrent(this IUserRepository users, User user, uint? expectedVersion) =>
        expectedVersion is null || users.VersionOf(user) == expectedVersion.Value;
}
