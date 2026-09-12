namespace UserManagementAPI.Api.Contracts.V1;

/// <summary>
/// The query string of GET /api/v1/users. Bound here, in the public contract,
/// and mapped to GetUsersQuery — the same boundary as request bodies and
/// commands (ADR 0011). Clamping and validation happen in the query.
/// </summary>
public sealed record UserQueryParameters
{
    public int PageNumber { get; init; } = 1;

    /// <summary>At most 50; larger values are clamped.</summary>
    public int PageSize { get; init; } = 10;

    /// <summary>Case-insensitive match on email, first name or last name.</summary>
    public string? SearchTerm { get; init; }

    /// <summary>Pending, Active, Locked or Deactivated.</summary>
    public string? Status { get; init; }

    /// <summary>For example "lastName desc, email". Fields: email, firstName, lastName, status, createdAt.</summary>
    public string? OrderBy { get; init; }
}