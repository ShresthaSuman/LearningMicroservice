

namespace CatalogAPI.Features.GetProductById;


//public record GetProductByIdRequest();
public record GetProductByIdResponse(Products Product);

public class GetProductByIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/products/{Id}", async (Guid Id,ISender sender) =>
        {
            var result = await sender.Send(new GetProductByIdQuery(Id));
            var response = result.Adapt<GetProductByIdResponse>();
            return Results.Ok(response);
        });
    }
}