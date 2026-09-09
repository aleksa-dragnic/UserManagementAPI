using UserManagementAPI.Domain.Common;

namespace UserManagementAPI.Domain.UnitTests.Common;

public class AggregateRootTests
{
    [Fact]
    public void DomainEvents_IsEmpty_OnANewAggregate()
    {
        var aggregate = new TestAggregate(Guid.NewGuid());

        aggregate.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void RaiseDomainEvent_AppendsToDomainEvents()
    {
        var aggregate = new TestAggregate(Guid.NewGuid());
        var domainEvent = new TestDomainEvent(aggregate.Id);

        aggregate.Raise(domainEvent);

        aggregate.DomainEvents.Should().ContainSingle().Which.Should().Be(domainEvent);
    }

    [Fact]
    public void ClearDomainEvents_EmptiesTheCollection()
    {
        var aggregate = new TestAggregate(Guid.NewGuid());
        aggregate.Raise(new TestDomainEvent(aggregate.Id));

        aggregate.ClearDomainEvents();

        aggregate.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void DomainEvents_CannotBeMutatedByACaller()
    {
        var aggregate = new TestAggregate(Guid.NewGuid());

        // The wrapper may implement ICollection<T> for interop; what matters is
        // that every mutation path is closed.
        var asCollection = (ICollection<IDomainEvent>)aggregate.DomainEvents;
        var act = () => asCollection.Add(new TestDomainEvent(aggregate.Id));

        asCollection.IsReadOnly.Should().BeTrue();
        act.Should().Throw<NotSupportedException>();
        aggregate.DomainEvents.Should().BeEmpty();
    }
}