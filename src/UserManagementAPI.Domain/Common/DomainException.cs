namespace UserManagementAPI.Domain.Common;

/// <summary>
/// Thrown when an invariant is violated on a path that cannot return a
/// <see cref="Result"/> — a property getter, a constructor, an operator.
/// It carries the same <see cref="Common.Error"/> a failed result would, so
/// the HTTP mapping is identical either way.
/// </summary>
public sealed class DomainException : Exception
{
    public DomainException(Error error)
        : base(error.Description) => Error = error;

    public Error Error { get; }
}