namespace UserManagementAPI.Api.Contracts.V1;

/// <summary>A user in a collection. Status is the lifecycle state by name: Pending, Active, Locked or Deactivated.</summary>
public sealed record UserResponse(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string Status);