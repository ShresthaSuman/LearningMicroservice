namespace CatalogAPI.Features.GetProductByCategory;

//public record GetProductByCategoryRequest(string category);
public record GetProductByIdResponse(IEnumerable<Products> products);
public class GetProductByCategoryEndPoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/products/category/{category}", async (string category, ISender sender) =>
        {
            var query = new GetProductByIdQuery(category);
            var request = await sender.Send(query);
            var response = request.Adapt<GetProductByIdResponse>();
            return Results.Ok(response);

        });
    }
}
