using UserManagementAPI.Application.Common;

namespace UserManagementAPI.Application.Users;

/// <summary>
/// What a user collection can be narrowed and ordered by. Every value here is
/// user-supplied; none of it reaches a query as text — status is matched
/// against the enumeration, the search term is a parameter, and the sort is
/// parsed against a whitelist of fields.
/// </summary>
public abstract record UserParameters : RequestParameters
{
    /// <summary>Matched case-insensitively against email, first name and last name.</summary>
    public string? SearchTerm { get; init; }

    /// <summary>A UserStatus name: Pending, Active, Locked or Deactivated.</summary>
    public string? Status { get; init; }

    /// <summary>
    /// Comma-separated "field [asc|desc]" clauses over email, firstName,
    /// lastName, status and createdAt. Anything else is ignored.
    /// </summary>
    public string? OrderBy { get; init; }
}