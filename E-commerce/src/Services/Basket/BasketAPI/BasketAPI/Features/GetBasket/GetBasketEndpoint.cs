using BasketAPI.Models;
using Carter;
using Mapster;
using MediatR;

namespace BasketAPI.Features.GetBasket;

public record GetBasketResponse(ShoppingCart Cart);
public class GetBasketEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/basket/{UserName}", async (string UserName, ISender sender) =>
        {
            var result = await sender.Send(new GetBasketQuery(UserName));
            var response = result.Adapt<GetBasketResponse>();
            return Results.Ok(response);
        })
        .WithName("Get Basket item")
        .WithDescription(("Returns all the basket items"))
        .Produces<GetBasketResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        
        
    }
}