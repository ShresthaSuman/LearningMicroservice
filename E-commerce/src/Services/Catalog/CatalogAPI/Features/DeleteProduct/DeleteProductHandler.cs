using BuildingBlocks.CQRS;

namespace CatalogAPI.Features.DeleteProduct;

public record DeleteProductCommand(Guid Id) :ICommand<DeleteProductResult>;

public record DeleteProductResult(string status);

public class DeleteProductValidator : AbstractValidator<DeleteProductCommand>
{
    public DeleteProductValidator()
    {
        RuleFor(p => p.Id).NotNull().WithMessage("Id cannot be null");
    }
}

public class DeleteProductHandler(IDocumentSession session): ICommandHandler<DeleteProductCommand, DeleteProductResult>
{
    public async Task<DeleteProductResult> Handle(DeleteProductCommand command, CancellationToken cancellationToken)
    {
       // logger.LogInformation("DeleteProductHandler handling command for {ProductId}", command.Id);
        session.Delete<Products>(command.Id);
        await session.SaveChangesAsync((cancellationToken));
        return new  DeleteProductResult("Deleted successfully");
    }
}