namespace UserManagementAPI.Application.Users.Dtos;

/// <summary>
/// A role assignment as read: the role's id and name, joined from the roles
/// table. The User aggregate itself only ever holds the id.
/// </summary>
public sealed record UserRoleDto(Guid RoleId, string Name, DateTime AssignedAtUtc);