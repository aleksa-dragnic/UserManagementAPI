namespace UserManagementAPI.Api.Contracts.V1;

/// <summary>A role and the permission codes it grants.</summary>
public sealed record RoleResponse(Guid Id, string Name, IReadOnlyList<string> Permissions);