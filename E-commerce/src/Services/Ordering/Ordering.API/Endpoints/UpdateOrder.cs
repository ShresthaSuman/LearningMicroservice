using Carter;
using Mapster;
using MediatR;
using Ordering.Application.Dtos;
using Ordering.Application.OrdersFeature.Commands.UpdateOrder;

namespace Ordering.API.Endpoints;

public record UpdateOrderRequest(OrderDto Order);
public record UpdateOrderResponse(bool isSuccess);
public class UpdateOrder : ICarterModule
{
    public  void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/orders", async (UpdateOrderRequest request, ISender sender) =>
            {
                var commend =   request.Adapt<UpdateOrderCommand>();
                var result = await sender.Send(commend);
                var response = result.Adapt<UpdateOrderResponse>();
                return Results.Ok(response);


            }).WithName("UpdateOrder")
            .Produces<UpdateOrderResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .WithDescription("Order updated successfully")
            .WithSummary("Update Order");
    }
}