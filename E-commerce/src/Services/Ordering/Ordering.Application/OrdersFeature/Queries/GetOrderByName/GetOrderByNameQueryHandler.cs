using Microsoft.EntityFrameworkCore;
using Ordering.Application.Extension;

namespace Ordering.Application.OrdersFeature.Queries.GetOrderByName;

public class GetOrderByNameQueryHandler(IApplicationDbContext dbContext): IQueryHandler<GetOrderByNameQuery,GetOrderByNameQueryResult>
{
    public async Task<GetOrderByNameQueryResult> Handle(GetOrderByNameQuery query, CancellationToken cancellationToken)
    {

        var orders = await dbContext.Orders
            .Include(o => o.OrderItems)
            .AsNoTracking()
            .Where(o => o.OrderName.Value.Contains(query.Name))
            .OrderBy(o => o.OrderName.Value)
            .ToListAsync(cancellationToken);
        
        
        
         //this can also be done but for reusability and maintainability we are creating an extension method
         //  var orderDtos = ProjectToOrderDto(orders);   
      
        return new GetOrderByNameQueryResult(orders.OrdersToOrderDtoList());
    }

    
    //this method also can be done this is store for a learning purpose 
    // now extension methos OrderToOrderDtoList has been created for this
    private List<OrderDto> ProjectToOrderDto(List<Order> orders)
    {
        List<OrderDto> result = new();

        foreach (var order in orders)
        {
            var orderDto = new OrderDto(
                Id: order.Id.Value,
                CustomerId: order.CustomerId.Value,
                OrderName: order.OrderName.Value,
                ShippingAddress: new AddressDto(
                    FirstName: order.ShippingAddress.FirstName,
                    LastName: order.ShippingAddress.LastName,
                    EmailAddress: order.ShippingAddress.EmailAddress,
                    AddressLine: order.ShippingAddress.AddressLine,
                    Country: order.ShippingAddress.Country,
                    State: order.ShippingAddress.State,
                    ZipCode: order.ShippingAddress.ZipCode
                ),
                BillingAddress: new AddressDto(
                    FirstName: order.BillingAddress.FirstName,
                    LastName: order.BillingAddress.LastName,
                    EmailAddress: order.BillingAddress.EmailAddress,
                    AddressLine: order.BillingAddress.AddressLine,
                    Country: order.BillingAddress.Country,
                    State: order.BillingAddress.State,
                    ZipCode: order.BillingAddress.ZipCode
                ),
                Payment: new PaymentDto(
                    CardName: order.Payment.CardName,
                    CardNumber: order.Payment.CardNumber,
                    Expiration: order.Payment.Expiration,
                    CVV: order.Payment.CVV,
                    PaymentMethod: order.Payment.PaymentMethod
                ),
                Status: order.Status,
                OrderItems: order.OrderItems
                        .Select(oi => new OrderItemDto(oi.OrderId.Value,
                                                       oi.ProductId.Value, 
                                                       oi.Quantity, 
                                                       oi.Price)).ToList()
            );
                result.Add(orderDto);
        }
        return result;
    }
    
}