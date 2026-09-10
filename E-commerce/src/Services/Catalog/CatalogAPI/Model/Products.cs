namespace CatalogAPI.Model;

public class Products
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string ImageFile { get; set; } = default!;
    public decimal Price { get; set; } 
    public List<string> Category { get; set; } = new List<string>();

}