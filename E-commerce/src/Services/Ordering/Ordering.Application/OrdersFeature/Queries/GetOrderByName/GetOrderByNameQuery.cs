namespace Ordering.Application.OrdersFeature.Queries.GetOrderByName;


public record GetOrderByNameQuery(string Name) : IQuery<GetOrderByNameQueryResult>;
public record GetOrderByNameQueryResult(IEnumerable<OrderDto> Orders);