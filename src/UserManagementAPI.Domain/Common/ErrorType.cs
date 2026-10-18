namespace UserManagementAPI.Domain.Common;

/// <summary>
/// What kind of failure an Error is. The HTTP layer chooses the status from
/// this, never from the wording of the code, so renaming a code cannot change
/// the status a client receives.
/// </summary>
public enum ErrorType
{
    /// <summary>The request is understood but breaks a rule. 400.</summary>
    Failure,

    /// <summary>The request has the wrong shape. 422.</summary>
    Validation,

    /// <summary>The caller could not be authenticated. 401.</summary>
    Unauthorized,

    /// <summary>The thing the request names does not exist. 404.</summary>
    NotFound,

    /// <summary>The request contradicts the current state. 409.</summary>
    Conflict,

    /// <summary>The state changed after the version the caller read. 412.</summary>
    PreconditionFailed
}
