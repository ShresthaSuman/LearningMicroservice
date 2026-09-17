namespace BasketAPI.Features.DeleteBasket;

public record DeleteBasketRequest(string UserName);
public record DeleteBasketResponse(bool success);
public class DeleteBasketEndpoint :ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete("/basket/{UserName}", async (string UserName, ISender sender) =>
            {
                var result = await sender.Send(new DeleteBasketCommand(UserName));
                var response = result.Adapt<DeleteBasketResponse>();
                return Results.Ok(response);
            })
            .WithName("DeleteBasket")
            .Produces<DeleteBasketResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Delete Product Basket")
            .WithDescription("Delete Product Basket");
    }
}