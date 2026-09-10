using BuildingBlocks.CQRS;


namespace CatalogAPI.Features.GetProduct;

public record GetProductQuery() : IQuery<GetProductResult>;
public record GetProductResult(IEnumerable<Products> Products);
internal class GetProductQueryHandler(IDocumentSession session, ILogger<GetProductQueryHandler> logger) : IQueryHandler<GetProductQuery,GetProductResult>
{
 public  async Task<GetProductResult> Handle(GetProductQuery query, CancellationToken cancellationToken)
 {
  logger.LogInformation("GetProductQueryHandler.Handle called with {@Query}");
  var products = await session.Query<Products>().ToListAsync(cancellationToken);
  return new GetProductResult(products);
 }
}