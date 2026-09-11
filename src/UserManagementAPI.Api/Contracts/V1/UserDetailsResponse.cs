namespace UserManagementAPI.Api.Contracts.V1;

/// <summary>A single user with the roles they hold.</summary>
public sealed record UserDetailsResponse(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string Status,
    IReadOnlyList<UserRoleResponse> Roles,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);