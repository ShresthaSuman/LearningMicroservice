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

public class UpdateProductValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductValidator()
    {
        RuleFor(p => p.Id).NotNull().WithMessage("Id cannot be null");
        RuleFor(p => p.Name).NotEmpty().WithMessage("Name cannot be null");
        RuleFor(p => p.Description).NotEmpty().WithMessage("Description cannot be empty");    
        RuleFor(p => p.Category).NotNull().WithMessage("Category cannot be null");  
        RuleFor(p => p.Price)
            .GreaterThan(0).WithMessage("Price cannot be less than 0")
            .NotNull().WithMessage("Price cannot be null");
    }
}
public class UpdateProductHandler(IDocumentSession session) : ICommandHandler<UpdateProductCommand , UpdateProductResult>
{
    public async Task<UpdateProductResult> Handle(UpdateProductCommand command, CancellationToken cancellationToken)
    {
      //  logger.LogInformation("UpdateProductHandler handling command for {ProductId}", command.Id);
        var product = await session.LoadAsync<Products>(command.Id, cancellationToken);
        if (product is null)
        {
            throw new System.Exception("Product not found");
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