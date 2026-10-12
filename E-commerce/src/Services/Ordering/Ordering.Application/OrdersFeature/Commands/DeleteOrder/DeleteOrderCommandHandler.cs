using Microsoft.EntityFrameworkCore;
using Ordering.Application.Exception;

namespace Ordering.Application.OrdersFeature.Commands.DeleteOrder;

public class DeleteOrderCommandHandler(IApplicationDbContext dbContext) :ICommandHandler<DeleteOrderCommand,DeleteOrderResult>
{
    public async Task<DeleteOrderResult> Handle(DeleteOrderCommand command, CancellationToken cancellationToken)
    { 
        var order = await dbContext.Orders.FindAsync([OrderId.Of(command.OrderId)], cancellationToken);
       if (order == null)
       {
           throw new OrderNotFoundException(command.OrderId);
       }
       
       dbContext.Orders.Remove(order);
       await dbContext.SaveChangesAsync(cancellationToken);
       return new DeleteOrderResult(true);
     
    }
}