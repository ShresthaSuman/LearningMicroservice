using System.Data;
using BasketAPI.Data;

namespace BasketAPI.Features.StoreBasket;


public record StoreBasketCommand(ShoppingCart Cart) : ICommand<StoreBasketResult>;

public record StoreBasketResult(string UserName);

public class ScoreBasketValidation : AbstractValidator<StoreBasketCommand>
{
    public ScoreBasketValidation()
    {
        RuleFor(c => c.Cart).NotNull().WithMessage("Cart is required");
        RuleFor(c => c.Cart.UserName).NotEmpty().WithMessage("User name is empty and is required");
    }
}
public class StoreBasketHandler(IBasketRepository repository)  : ICommandHandler<StoreBasketCommand,StoreBasketResult>
{
    public async Task<StoreBasketResult> Handle(StoreBasketCommand command, CancellationToken cancellationToken)
    {
        ShoppingCart cart = command.Cart;
        await repository.StoreBasket(cart,cancellationToken);
        return new StoreBasketResult(cart.UserName);

    }
}