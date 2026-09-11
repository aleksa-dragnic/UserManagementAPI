namespace UserManagementAPI.Api.Contracts.V1;

/// <summary>
/// The public, versioned shape of a registration. It is not the command: when
/// v2 changes this, the command and the domain do not move (ADR 0011).
/// </summary>
public sealed record RegisterUserRequest(
    string Email,
    string FirstName,
    string LastName,
    string Password);