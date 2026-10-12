using Ordering.Domain.Abstraction;
using Ordering.Domain.ValueObjects;

namespace Ordering.Domain.Model;

public class OrderItem : Entity<OrderItemId>
{
    
  
    public OrderId OrderId { get; private set; } = default!;
    public ProductId ProductId { get; private set; } = default!;
    public int Quantity { get; private set; } = default!;
    public decimal Price { get; private set; } = default!;

    internal OrderItem(OrderId orderId, ProductId productId, int quantity, decimal price)
    {
        Id = OrderItemId.Of(Guid.NewGuid()); // this Id is Entity Id from IEntity. Dont be confuse on whee this Id came from
        OrderId = orderId;
        ProductId = productId;
        Quantity = quantity;
        Price = price;
    }
}