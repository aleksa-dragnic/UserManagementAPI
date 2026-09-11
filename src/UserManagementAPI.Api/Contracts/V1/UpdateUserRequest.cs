namespace UserManagementAPI.Api.Contracts.V1;

public sealed record UpdateUserRequest(
    string Email,
    string FirstName,
    string LastName);