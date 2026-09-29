using Ordering.Domain.Abstraction;
using Ordering.Domain.Model;

namespace Ordering.Domain.Events;

public record OrderCreatedEvent(Order order) : IDomainEvent
{
    
}