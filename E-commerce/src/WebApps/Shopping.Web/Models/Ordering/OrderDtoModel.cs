namespace Shopping.Web.Models.Ordering;

public record OrderDtoModel(
    Guid Id,
    Guid CustomerId,
    string OrderName,
    AddressDtoModel ShippingAddress,
    AddressDtoModel BillingAddress,
    PaymentDtoModel Payment,
    OrderStatus Status,
    List<OrderItemDtoModel>  OrderItems
    );
    
public enum OrderStatus
{
    Draft = 1,
    Pending = 2,
    Completed = 3,
    Canceled  = 4,
   
}


public record GetOrderResponse(PaginationResult<OrderDtoModel> Orders);
public record GetOrderByNameResponse(IEnumerable<OrderDtoModel> Orders);
public record GetOrderByCustomerResponse(Guid Id);
