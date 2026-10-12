namespace Ordering.Application.OrdersFeature.Queries.GetOrderByCustomer;
public record GetOrderByCustomerQuery(Guid CustomerId) : IQuery<GetOrderByCustomerQueryResult>;
public record GetOrderByCustomerQueryResult(IEnumerable<OrderDto> Orders);