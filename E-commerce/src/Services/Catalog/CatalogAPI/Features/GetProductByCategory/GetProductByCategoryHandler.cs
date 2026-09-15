namespace CatalogAPI.Features.GetProductByCategory;

public record GetProductByIdQuery(string category) : IQuery<GetProductByIdQueryResult>;
public record GetProductByIdQueryResult(IEnumerable<Products> products);

public class GetProductByCategoryHandler(IDocumentSession session) : IQueryHandler<GetProductByIdQuery,GetProductByIdQueryResult>
{
    public async Task<GetProductByIdQueryResult> Handle(GetProductByIdQuery query, CancellationToken cancellationToken)
    {
       var result = await session.Query<Products>()
           .Where(p => p.Category.Contains(query.category))
           .ToListAsync(cancellationToken);
       return new GetProductByIdQueryResult(result);
       
    }
}