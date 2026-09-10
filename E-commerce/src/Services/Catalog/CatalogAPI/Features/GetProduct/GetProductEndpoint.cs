
namespace CatalogAPI.Features.GetProduct;


public record GetProductRequest();
public record GetProductResponse(IEnumerable<Products> Products);
public class GetProductEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/products", async (ISender sender) =>
            {
                var result = await sender.Send(new GetProductQuery());
                var response = result.Adapt<GetProductResponse>();
                return Results.Ok(response);
            })
            .WithName("Get products")
            .WithDescription(("Returns all the products"))
            .Produces<GetProductResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);
       
    }
}