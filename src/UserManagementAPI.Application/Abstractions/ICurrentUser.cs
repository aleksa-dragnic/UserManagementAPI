namespace UserManagementAPI.Application.Abstractions;

/// <summary>
/// Who is making the current request. Implemented in Api from the HTTP context
/// (M4 PR17); defined here so Application code can ask without knowing about
/// HTTP.
/// </summary>
public interface ICurrentUser
{
    /// <summary>The authenticated user's id, or null for an anonymous or system caller.</summary>
    Guid? UserId { get; }

    bool IsAuthenticated { get; }
}