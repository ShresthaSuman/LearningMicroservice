

namespace CatalogAPI.Features.GetProductById;

public record GetProductByIdQuery(Guid Id) : IQuery<GetProductByIdQueryResult>;

public record GetProductByIdQueryResult(Products Product);

internal class GetProductByIdQueryHandler(IDocumentSession session) : IQueryHandler<GetProductByIdQuery, GetProductByIdQueryResult>
{
    public async Task<GetProductByIdQueryResult> Handle(GetProductByIdQuery query, CancellationToken cancellationToken)
    {
        
        if (query.Id == Guid.Empty)
        {
            throw new KeyNotFoundException("Product not found");
        }
        var product = await session.LoadAsync<Products>(query.Id, cancellationToken);
        if (product is null)
        {
            throw new ProductNotFoundException("Product not found");
        }
        return new GetProductByIdQueryResult(product);
    }
}