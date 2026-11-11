using Refit;
using Shopping.Web.Models.Category;

namespace Shopping.Web.Services;

public interface ICatalogService
{
    [Get("/catalog-service/products?pageNumber={pageNumber}&pageSize={pageSize}")]
    Task<GetProductResponse> GetProducts(int? page = 1, int? pageSize = 10);
    
    [Get("/catalog-service/products/{Id}")]
    Task<GetProductByIdResponse> GetProductById(Guid Id);
    
    [Get("/catalog-service/products/category/{category}")]
    Task<GetProductByCategoryResponse> GetProductByCategory(string category);
    
}