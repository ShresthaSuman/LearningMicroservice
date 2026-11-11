namespace Shopping.Web.Models.Category;

public class ProductsModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string ImageFile { get; set; } = default!;
    public decimal Price { get; set; } 
    public List<string> Category { get; set; } = new List<string>();

}

public record GetProductResponse(IEnumerable<ProductsModel> Products);
public record GetProductByCategoryResponse(IEnumerable<ProductsModel> Products);
public record GetProductByIdResponse(ProductsModel Product);