namespace UserManagementAPI.Api.Contracts.V1;

/// <summary>
/// One change to a user (ADR 0025). <c>action</c> is <c>registered</c>,
/// <c>updated</c>, <c>deleted</c>, <c>roleAssigned</c> or <c>roleRemoved</c>.
/// <c>fields</c> names what an update changed (<c>email</c>, <c>firstName</c>,
/// <c>lastName</c>, <c>password</c>, <c>status</c>) and never carries a value.
/// The role is set for a role change; the actor is null for a system write, and
/// its email null if that user no longer exists.
/// </summary>
public sealed record UserHistoryEntryResponse(
    DateTime OccurredAtUtc,
    string Action,
    IReadOnlyList<string> Fields,
    Guid? RoleId,
    string? RoleName,
    Guid? ActorId,
    string? ActorEmail);
