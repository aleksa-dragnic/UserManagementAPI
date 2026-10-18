namespace UserManagementAPI.Api.Contracts.V1;

/// <summary>
/// A single user with the roles they hold. <c>Version</c> changes on every write;
/// send it back quoted in If-Match to refuse a write over a newer state (ADR 0021).
/// </summary>
public sealed record UserDetailsResponse(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string Status,
    IReadOnlyList<UserRoleResponse> Roles,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    string Version);