using BasketAPI.Data;
using BasketAPI.Models;
using BuildingBlocks.CQRS;

namespace BasketAPI.Features.GetBasket;


public record GetBasketQuery (string UserName) : IQuery<GetBasketResult>;
public record GetBasketResult(ShoppingCart Cart);
public class GetBasketHandler(IBasketRepository repository) : IQueryHandler<GetBasketQuery, GetBasketResult>
{
    public async Task<GetBasketResult> Handle(GetBasketQuery request, CancellationToken cancellationToken)
    {
       
        var basket =await  repository.GetBasket(request.UserName);
       
       return new GetBasketResult(basket);
    }
}