using System.Reflection;

namespace UserManagementAPI.Domain.Common;

/// <summary>
/// A closed set of named domain states. Unlike a C# enum this is a real object,
/// so a state can carry behaviour and cannot be conjured from an arbitrary int
/// cast. Instances are declared as public static readonly fields on the derived
/// type, which is what <see cref="GetAll{T}"/> reflects over.
/// </summary>
public abstract class Enumeration : IEquatable<Enumeration>, IComparable<Enumeration>
{
    protected Enumeration(int id, string name)
    {
        Id = id;
        Name = name;
    }

    public int Id { get; }

    public string Name { get; }

    public static IReadOnlyCollection<T> GetAll<T>()
        where T : Enumeration =>
        typeof(T)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(field => field.FieldType == typeof(T))
            .Select(field => (T)field.GetValue(null)!)
            .ToList();

    public static T FromId<T>(int id)
        where T : Enumeration =>
        GetAll<T>().FirstOrDefault(item => item.Id == id)
        ?? throw new InvalidOperationException($"'{id}' is not a valid {typeof(T).Name} id.");

    public static T FromName<T>(string name)
        where T : Enumeration =>
        GetAll<T>().FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException($"'{name}' is not a valid {typeof(T).Name} name.");

    public static bool operator ==(Enumeration? left, Enumeration? right) => Equals(left, right);

    public static bool operator !=(Enumeration? left, Enumeration? right) => !Equals(left, right);

    public bool Equals(Enumeration? other) =>
        other is not null && other.GetType() == GetType() && other.Id == Id;

    public override bool Equals(object? obj) => obj is Enumeration enumeration && Equals(enumeration);

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);

    public int CompareTo(Enumeration? other) => other is null ? 1 : Id.CompareTo(other.Id);

    public override string ToString() => Name;
}