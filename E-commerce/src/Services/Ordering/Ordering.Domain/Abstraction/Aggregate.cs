namespace Ordering.Domain.Abstraction;

public  abstract class Aggregate<TId> : Entity<TId>, IAggregate<TId>
{

    private readonly List<IDomainEvent> _domainEvent = new();
    public IReadOnlyList<IDomainEvent> DomainEvent => _domainEvent.AsReadOnly();
    
    
    public IDomainEvent[] ClearDomainEvents()
    {
        IDomainEvent[] dequeuedEvents = _domainEvent.ToArray();
        _domainEvent.Clear();
        return dequeuedEvents;
    }

    public void AddDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvent.Add(domainEvent);
    }
}