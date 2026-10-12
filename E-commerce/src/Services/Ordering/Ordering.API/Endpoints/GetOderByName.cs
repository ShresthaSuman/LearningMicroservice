using Carter;
using Mapster;
using MediatR;
using Ordering.Application.Dtos;
using Ordering.Application.OrdersFeature.Queries.GetOrderByName;

namespace Ordering.API.Endpoints;


public record GetOrderByNameResponse(IEnumerable<OrderDto> Orders);
public class GetOrderByName : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/orders/{orderName}", async (string orderName, ISender sender) =>
            {
                var result = await sender.Send(new GetOrderByNameQuery(orderName));
                var response = result.Adapt<GetOrderByNameResponse>();
                return Results.Ok(response);
            })
            .WithName("GetOrderByName")
            .WithDescription("Gets order by name")
            .WithSummary("Gets order by name")
            .Produces<GetOrderByNameResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}