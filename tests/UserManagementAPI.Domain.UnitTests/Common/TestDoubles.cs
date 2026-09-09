using UserManagementAPI.Domain.Common;

namespace UserManagementAPI.Domain.UnitTests.Common;

/// <summary>
/// Minimal concrete types used to exercise the abstract seedwork. They live in
/// one file because none of them carries behaviour worth testing on its own.
/// </summary>
internal sealed class TestEntity(Guid id) : Entity(id);

internal sealed class OtherTestEntity(Guid id) : Entity(id);

internal sealed record TestDomainEvent(Guid EntityId) : IDomainEvent
{
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}

internal sealed class TestAggregate(Guid id) : AggregateRoot(id)
{
    public void Raise(IDomainEvent domainEvent) => RaiseDomainEvent(domainEvent);
}

internal sealed class TestValueObject(string first, int second) : ValueObject
{
    public string First { get; } = first;

    public int Second { get; } = second;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return First;
        yield return Second;
    }
}

internal sealed class OtherTestValueObject(string first, int second) : ValueObject
{
    public string First { get; } = first;

    public int Second { get; } = second;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return First;
        yield return Second;
    }
}

internal sealed class TestEnumeration : Enumeration
{
    public static readonly TestEnumeration First = new(1, "First");
    public static readonly TestEnumeration Second = new(2, "Second");

    private TestEnumeration(int id, string name) : base(id, name)
    {
    }
}