namespace UserManagementAPI.Domain.Common;

/// <summary>
/// Base type for a concept defined entirely by its values. No identity, no
/// lifecycle: two instances with the same components are interchangeable.
/// </summary>
public abstract class ValueObject : IEquatable<ValueObject>
{
    /// <summary>
    /// The values that define this instance, in a stable order. Everything that
    /// participates in equality is yielded here and nowhere else.
    /// </summary>
    protected abstract IEnumerable<object?> GetEqualityComponents();

    public static bool operator ==(ValueObject? left, ValueObject? right) => Equals(left, right);

    public static bool operator !=(ValueObject? left, ValueObject? right) => !Equals(left, right);

    public bool Equals(ValueObject? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (other.GetType() != GetType())
        {
            return false;
        }

        return GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
    }

    public override bool Equals(object? obj) => obj is ValueObject valueObject && Equals(valueObject);

    public override int GetHashCode()
    {
        var hashCode = new HashCode();

        foreach (var component in GetEqualityComponents())
        {
            hashCode.Add(component);
        }

        return hashCode.ToHashCode();
    }
}