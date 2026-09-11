namespace UserManagementAPI.Application.Users.Dtos;

/// <summary>
/// A user as the read side sees it: flat, no behaviour, projected straight from
/// the users table. Queries return it and the Api maps it to its own versioned
/// contract — it is never serialized itself (ADR 0011).
/// </summary>
public sealed record UserDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string Status);