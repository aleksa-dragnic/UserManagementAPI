namespace UserManagementAPI.Application.Users.Dtos;

/// <summary>A single user with the roles they hold and the audit timestamps.</summary>
public sealed record UserDetailsDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string Status,
    IReadOnlyList<UserRoleDto> Roles,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);