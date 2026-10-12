using BuilfingBlocks.Pagination;
using Microsoft.EntityFrameworkCore;
using Ordering.Application.Extension;

namespace Ordering.Application.OrdersFeature.Queries.GetOrder;

public class GetOrderQueryHandler(IApplicationDbContext dbContext) : IQueryHandler<GetOrderQuery,GetOrderQueryResult>
{
    public async Task<GetOrderQueryResult> Handle(GetOrderQuery query, CancellationToken cancellationToken)
    {
        var pageIndex = query.PaginationRequest.pageIndex;
        var pageSize = query.PaginationRequest.pageSize;
        var totalCount = await dbContext.Orders.LongCountAsync(cancellationToken);

        var orders = await dbContext.Orders
            .Include(o => o.OrderItems)
            .AsNoTracking()
            .OrderBy(o=>o.OrderName.Value)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new GetOrderQueryResult(new PaginationResult<OrderDto>
        (
            pageIndex,
            pageSize,
            totalCount,
            orders.OrdersToOrderDtoList()
        ));
    
    }
}