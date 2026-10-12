using Ordering.Domain.Exception;

namespace Ordering.Domain.ValueObjects;

public record OrderItemId
{
    public Guid Value { get; }
    private OrderItemId(Guid value) => Value = value;
    public static OrderItemId Of(Guid value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value == Guid.Empty)
        {
            throw new DomainException("CustomerId is empty");
        }

        return new OrderItemId(value);
    }
    
    //Note : Yes, exactly right. Just one small wording tweak on the second:
    // 
    // Lambda	Direction	What happens
    // orderItemId => orderItemId.Value	Write (domain → DB)	Unwraps the OrderItemId to a plain Guid so EF can store it in the column
    // dbId => OrderItemId.Of(dbId)	Read (DB → domain)	Takes the plain Guid from the column and wraps it back into an OrderItemId via your factory
    // 
    // So:
    // 
    // Saving: OrderItemId → Guid → column
    // Loading: column → Guid → OrderItemId
    // The database never sees OrderItemId — only the raw Guid. The domain layer never sees the raw Guid — only OrderItemId. EF Core is the translator in the middle.
}