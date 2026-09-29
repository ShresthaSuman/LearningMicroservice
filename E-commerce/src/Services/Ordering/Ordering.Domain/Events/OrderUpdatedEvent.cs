using Ordering.Domain.Abstraction;
using Ordering.Domain.Model;

namespace Ordering.Domain.Events;

public record OrderUpdatedEvent(Order order) : IDomainEvent
{
    
}