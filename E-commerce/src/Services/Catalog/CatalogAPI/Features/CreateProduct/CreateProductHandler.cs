using BuildingBlocks.CQRS;
using FluentValidation;
using ValidationException = System.ComponentModel.DataAnnotations.ValidationException;


namespace CatalogAPI.Features;

public record CreateProductCommand(string Name, string Description, decimal Price,string ImageFile, List<string> Category) : ICommand<CreateProductResult>;

public record CreateProductResult(Guid Id);

public class CreatProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreatProductCommandValidator()
    {
        RuleFor(x => x.Name).NotNull().NotEmpty().WithMessage("Product name is required");
        RuleFor(x => x.Description).NotNull().NotEmpty().WithMessage("Description is required");
        RuleFor(x => x.Price)
            .NotNull().WithMessage("Price is required")
            .GreaterThan(0).WithMessage("Price must be grater than 0");
        
        RuleFor(x => x.Category).NotNull().NotEmpty().WithMessage("Category is required");
        RuleFor(x => x.ImageFile).NotNull().NotEmpty().WithMessage("ImageFile is required");
    }
}

public class CreateProductHandler(IDocumentSession session)
    : ICommandHandler<CreateProductCommand,CreateProductResult>
{
    public  async Task<CreateProductResult> Handle(CreateProductCommand command, CancellationToken cancellationToken)
    {

        // This is not a good practise to use validator individually so we have to create generic validator 
     /* var result = await validator.ValidateAsync(command, cancellationToken);
       var errors = result.Errors.Select(x => x.ErrorMessage).ToList();
       if (errors.Any())
       {
           throw new ValidationException(errors.FirstOrDefault());
       }*/
     
     
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