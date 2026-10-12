using BuilfingBlocks.Pagination;
using Carter;
using Mapster;
using MediatR;
using Ordering.Application.Dtos;
using Ordering.Application.OrdersFeature.Queries.GetOrder;
using Ordering.Application.OrdersFeature.Queries.GetOrderByName;

namespace Ordering.API.Endpoints;

public record GetOrderRequest(PaginationRequest Request);
public record GetOrderResponse(PaginationResult<OrderDto> Orders);
public class GetOrder : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/orders/", async ([AsParameters] PaginationRequest request, ISender sender) =>
            {
                var result = await sender.Send(new GetOrderQuery(request));
                var response = result.Adapt<GetOrderResponse>();
                return Results.Ok(response);
            })
            .WithName("GetOrder")
            .WithDescription("Gets orderlist")
            .WithSummary("Gets order ")
            .Produces<GetOrderResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}