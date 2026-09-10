using BuildingBlocks.CQRS;


namespace CatalogAPI.Features;

public record CreateProductCommand(string Name, string Description, decimal Price,string ImageFile, List<string> Category) : ICommand<CreateProductResult>;

public record CreateProductResult(Guid Id);
public class CreateProductHandler(IDocumentSession session): ICommandHandler<CreateProductCommand,CreateProductResult>
{
    public  async Task<CreateProductResult> Handle(CreateProductCommand command, CancellationToken cancellationToken)
    {

        var product = new Products()
        {
            Id = Guid.NewGuid(),
            Name = command.Name,
            Description = command.Description,
            Price = command.Price,
            ImageFile = command.ImageFile,
            Category = command.Category
        };
        session.Store(product);
        await session.SaveChangesAsync(cancellationToken);
        return new CreateProductResult(product.Id);

    }
}