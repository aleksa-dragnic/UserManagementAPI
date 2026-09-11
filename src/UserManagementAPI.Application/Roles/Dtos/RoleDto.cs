namespace UserManagementAPI.Application.Roles.Dtos;

/// <summary>A role and the permission codes it holds, ordered by code.</summary>
public sealed record RoleDto(Guid Id, string Name, IReadOnlyList<string> Permissions);