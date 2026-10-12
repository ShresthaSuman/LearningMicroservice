using BuilfingBlocks.Pagination;

namespace Ordering.Application.OrdersFeature.Queries.GetOrder;

public record GetOrderQuery(PaginationRequest PaginationRequest) : IQuery<GetOrderQueryResult>;
public record GetOrderQueryResult(PaginationResult<OrderDto>  orders);