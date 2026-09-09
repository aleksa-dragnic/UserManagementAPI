namespace UserManagementAPI.Domain.Common;

/// <summary>
/// Entry point to an aggregate and the only place domain events are raised.
/// The event list is exposed read-only: callers observe what happened, they do
/// not decide what happened.
/// </summary>
public abstract class AggregateRoot : Entity
{
    private readonly List<IDomainEvent> _domainEvents = [];

    protected AggregateRoot(Guid id) : base(id)
    {
    }

    protected AggregateRoot()
    {
    }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void RaiseDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    /// <summary>Called by the persistence layer after the events have been dispatched.</summary>
    public void ClearDomainEvents() => _domainEvents.Clear();
}