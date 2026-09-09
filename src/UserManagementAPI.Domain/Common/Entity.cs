namespace UserManagementAPI.Domain.Common;

/// <summary>
/// Base type for anything with an identity that persists across state changes.
/// Two entities are the same entity when they are the same concrete type and
/// carry the same id — reference equality is irrelevant once a row round-trips
/// through the database.
/// </summary>
public abstract class Entity : IEquatable<Entity>
{
    protected Entity(Guid id) => Id = id;

    /// <summary>Required by EF Core, which materialises through a parameterless constructor.</summary>
    protected Entity()
    {
    }

    public Guid Id { get; protected set; }

    public static bool operator ==(Entity? left, Entity? right) => Equals(left, right);

    public static bool operator !=(Entity? left, Entity? right) => !Equals(left, right);

    public bool Equals(Entity? other)
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

        return other.Id == Id;
    }

    public override bool Equals(object? obj) => obj is Entity entity && Equals(entity);

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}