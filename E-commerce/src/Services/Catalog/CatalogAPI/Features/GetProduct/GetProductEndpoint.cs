
namespace CatalogAPI.Features.GetProduct;


public record GetProductsRequest(int? PageNumber = 1, int? PageSize = 10);
public record GetProductResponse(IEnumerable<Products> Products);
public class GetProductEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/products", async ([AsParameters] GetProductsRequest request,ISender sender) =>
            {
                var query = request.Adapt<GetProductQuery>();
                var result = await sender.Send(query);
                var response = result.Adapt<GetProductResponse>();
                return Results.Ok(response);
            })
            .WithName("Get products")
            .WithDescription(("Returns all the products"))
            .Produces<GetProductResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);
       
    }
}