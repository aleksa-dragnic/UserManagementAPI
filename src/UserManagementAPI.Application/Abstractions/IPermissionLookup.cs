namespace UserManagementAPI.Application.Abstractions;

/// <summary>
/// The permission codes a user currently holds through their roles. A read, so
/// it does not go through the aggregates — User holds RoleIds, Role holds
/// PermissionIds, and resolving the codes is a join the persistence layer does
/// in one query.
/// </summary>
public interface IPermissionLookup
{
    Task<IReadOnlyCollection<string>> GetPermissionCodesAsync(Guid userId, CancellationToken cancellationToken = default);
}