namespace Shopping.Web.Models.Basket;

public class ShoppingCartModel
{
    public string UserName { get; set; } = default!;
    public  List<ShoppingCartItemModel> Items { get; set; } = new List<ShoppingCartItemModel>();
    public decimal TotalPrice =>  Items.Sum(x=>x.Price*x.Quantity);
}

public record StoreBasketRequest(ShoppingCartModel Cart);
public record StoreBasketResponse(string UserName);

public record GetBasketResponse(ShoppingCartModel Cart);
public record DeleteBasketResponse(bool Success);