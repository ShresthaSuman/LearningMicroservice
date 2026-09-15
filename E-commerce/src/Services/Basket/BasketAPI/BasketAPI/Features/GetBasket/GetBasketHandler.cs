using BasketAPI.Models;
using BuildingBlocks.CQRS;

namespace BasketAPI.Features.GetBasket;


public record GetBasketQuery (string UserName) : IQuery<GetBasketResult>;
public record GetBasketResult(ShoppingCart Cart);
public class GetBasketHandler : IQueryHandler<GetBasketQuery, GetBasketResult>
{
    public async Task<GetBasketResult> Handle(GetBasketQuery request, CancellationToken cancellationToken)
    {
       // 
       
       return new GetBasketResult(new ShoppingCart("svn"));
    }
}