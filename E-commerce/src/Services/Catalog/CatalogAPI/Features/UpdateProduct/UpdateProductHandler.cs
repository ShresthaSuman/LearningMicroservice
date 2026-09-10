using BuildingBlocks.CQRS;

namespace CatalogAPI.Features.UpdateProduct;


public record UpdateProductCommand(
    Guid Id,
    string Name,
    string Description,
    string ImageFile,
    decimal Price,
    List<string> Category) : ICommand<UpdateProductResult>;

public record UpdateProductResult(bool success);
public class UpdateProductHandler(IDocumentSession session,ILogger<UpdateProductHandler> logger) : ICommandHandler<UpdateProductCommand , UpdateProductResult>
{
    public async Task<UpdateProductResult> Handle(UpdateProductCommand command, CancellationToken cancellationToken)
    {
        logger.LogInformation("UpdateProductHandler handling command for {ProductId}", command.Id);
        var product = await session.LoadAsync<Products>(command.Id, cancellationToken);
        if (product is null)
        {
            throw new Exception("Product not found");
        }
        
        product.Name = command.Name;
        product.Description = command.Description;
        product.ImageFile = command.ImageFile;
        product.Price = command.Price;
        product.Category = command.Category;

      
         session.Update(product);
         await session.SaveChangesAsync(cancellationToken);
         return new UpdateProductResult(true);
    }
}