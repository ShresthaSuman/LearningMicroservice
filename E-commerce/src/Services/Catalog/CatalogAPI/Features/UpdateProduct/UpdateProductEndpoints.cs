namespace CatalogAPI.Features.UpdateProduct;

public record UpdateProductRequest(
    Guid Id,
    string Name,
    string Description,
    string ImageFile,
    decimal Price,
    List<string> Category);

public record UpdateProductResponse(bool success);

public class UpdateProductEndpoints : ICarterModule
{
    public  void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/products/{id:guid}", async (Guid id, UpdateProductRequest request, ISender sender) =>
        {
            var command = new UpdateProductCommand(
                id,
                request.Name,
                request.Description,
                request.ImageFile,
                request.Price,
                request.Category);
            var result= await  sender.Send(command);
            var response = result.Adapt<UpdateProductResponse>();
            return Results.Ok(response);
        })
        .WithName("UpdateProduct")
        .Produces<UpdateProductResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Update Product")
        .WithDescription("Update Product");
    }
}