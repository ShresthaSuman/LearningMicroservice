
namespace BasketAPI.Data;

public class BasketRepository(IDocumentSession session) :IBasketRepository
{
    public  async Task<ShoppingCart> GetBasket(string username, CancellationToken cancellationToken = default)
    {
        var basket = await session.LoadAsync<ShoppingCart>(username, cancellationToken);
        if (basket == null)
            throw new BasketNotFoundException(username);
        return basket;
    }

    public async Task<ShoppingCart> StoreBasket(ShoppingCart basket, CancellationToken cancellationToken = default)
    {
        session.Store(basket);
        await session.SaveChangesAsync(cancellationToken);
        return basket;
       
    }

    public async Task<bool> DeleteBasket(string username, CancellationToken cancellationToken = default)
    {
        session.Delete<ShoppingCart>(username);
        await session.SaveChangesAsync(cancellationToken);
        return true;
    }
}