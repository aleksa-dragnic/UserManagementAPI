namespace UserManagementAPI.Application.Users.Dtos;

/// <summary>
/// One change to a user, as the audit log recorded it (ADR 0025): what kind of
/// change, which fields by name - never their values - the role it concerns,
/// and who made it. A write with no actor (the seeder) has neither id nor email.
/// </summary>
public sealed record UserHistoryEntryDto(
    DateTime OccurredAtUtc,
    string Action,
    IReadOnlyList<string> Fields,
    Guid? RoleId,
    string? RoleName,
    Guid? ActorId,
    string? ActorEmail);

/// <summary>The kinds of change a user's history reports.</summary>
public static class UserHistoryActions
{
    public const string Registered = "registered";

    public const string Updated = "updated";

    public const string Deleted = "deleted";

    public const string RoleAssigned = "roleAssigned";

    public const string RoleRemoved = "roleRemoved";
}
