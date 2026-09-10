namespace CatalogAPI.Exception;

public class ProductNotFoundException : System.Exception
{
    public ProductNotFoundException(String message) : base(message)
    {
        
    }
}