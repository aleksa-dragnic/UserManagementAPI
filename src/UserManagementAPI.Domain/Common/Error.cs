namespace UserManagementAPI.Domain.Common;

/// <summary>
/// A failure the domain expects. The code is stable and machine-readable, the
/// type decides the HTTP status, and the description is for a human reading a
/// log. An error that names no type is a Failure.
///
/// Not sealed: the application layer derives a validation error from it that
/// carries one entry per failed property, so a failed Result can still be a
/// single Error and the HTTP layer can still map on its type.
/// </summary>
public record Error(string Code, string Description, ErrorType Type = ErrorType.Failure)
{
    public static readonly Error None = new(string.Empty, string.Empty);

    public static readonly Error NullValue = new(
        "General.NullValue",
        "A null value was provided where one is not allowed.");
}