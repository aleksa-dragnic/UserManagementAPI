namespace UserManagementAPI.Api.Contracts.V1;

public sealed record LoginRequest(string Email, string Password);