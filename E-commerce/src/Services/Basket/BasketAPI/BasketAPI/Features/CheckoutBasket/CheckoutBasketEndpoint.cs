namespace BasketAPI.Features.CheckoutBasket;


public record CheckoutBasketRequest(BasketCheckoutDto BasketCheckoutDto);

public record CheckoutBasketResponse(bool isSuccess);
public class CheckoutBasketEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
       app.MapPost("/basket/checkout",async (CheckoutBasketRequest request,ISender sender)=>
       {
           var command = request.Adapt<CheckoutBasketCommand>();
           var result = await sender.Send(command);
           var response = result.Adapt<CheckoutBasketResponse>();
           return Results.Ok(response);
       })
       .WithName("CheckoutBasketEndpoint")
       .ProducesProblem(statusCode:StatusCodes.Status400BadRequest)
       .WithSummary("Checkout basket")
       .WithDescription("Checkout basket")
       .Produces<CheckoutBasketResponse>(StatusCodes.Status201Created);
    }
}