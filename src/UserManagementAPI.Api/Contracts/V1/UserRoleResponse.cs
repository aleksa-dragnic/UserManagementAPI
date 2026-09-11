namespace UserManagementAPI.Api.Contracts.V1;

public sealed record UserRoleResponse(Guid RoleId, string Name, DateTime AssignedAtUtc);