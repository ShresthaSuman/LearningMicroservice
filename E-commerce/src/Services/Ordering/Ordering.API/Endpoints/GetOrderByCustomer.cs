using Carter;
using Mapster;
using MediatR;
using Ordering.Application.Dtos;
using Ordering.Application.OrdersFeature.Queries.GetOrderByCustomer;

namespace Ordering.API.Endpoints;


public record GetOrderByCustomerResponse(IEnumerable<OrderDto> Orders);
public class GetOrderByCustomer:ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("orders/customer/{customerId}", async (Guid customerId, ISender sender) =>
            {
                var request = await sender.Send(new GetOrderByCustomerQuery(customerId));
                var response = request.Adapt<GetOrderByCustomerResponse>();
                return Results.Ok(response);
            })
            .WithName("GetOrderByCustomer")
            .WithDescription("Gets order by name")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .Produces<GetOrderByCustomer>(StatusCodes.Status200OK);
    }
}