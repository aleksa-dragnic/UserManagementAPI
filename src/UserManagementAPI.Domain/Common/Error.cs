namespace UserManagementAPI.Domain.Common;

/// <summary>
/// A failure the domain expects. The code is stable and machine-readable — the
/// HTTP layer maps on it — while the description is for a human reading a log.
///
/// Not sealed: the application layer derives a validation error from it that
/// carries one entry per failed property, so a failed Result can still be a
/// single Error and the HTTP layer can still map on the code.
/// </summary>
public record Error(string Code, string Description)
{
    public static readonly Error None = new(string.Empty, string.Empty);

    public static readonly Error NullValue = new(
        "General.NullValue",
        "A null value was provided where one is not allowed.");
}