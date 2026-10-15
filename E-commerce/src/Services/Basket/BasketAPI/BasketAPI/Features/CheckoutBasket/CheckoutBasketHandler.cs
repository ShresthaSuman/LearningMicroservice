

using BasketAPI.Data;
using BuildingBlocks.Messaging.Events;
using MassTransit;

namespace BasketAPI.Features.CheckoutBasket;


public record CheckoutBasketCommand(BasketCheckoutDto BasketCheckoutDto) : ICommand<CheckoutBasketResult>;

public record CheckoutBasketResult(bool isSuccess);

public class CheckoutBasketHandlerValidator : AbstractValidator<CheckoutBasketCommand>
{
    public CheckoutBasketHandlerValidator()
    {
        RuleFor(command => command.BasketCheckoutDto).NotNull().WithMessage("Please specify a BasketCheckoutDto");
        RuleFor(command =>command.BasketCheckoutDto.UserName).NotEmpty().WithMessage("Please specify a UserName");
    }
}
public class CheckoutBasketHandler(IBasketRepository repository, IPublishEndpoint publishEndpoint) :ICommandHandler<CheckoutBasketCommand,CheckoutBasketResult>
{
    public async Task<CheckoutBasketResult> Handle(CheckoutBasketCommand command, CancellationToken cancellationToken)
    {
        var basket = await repository.GetBasket(command.BasketCheckoutDto.UserName,cancellationToken);
        if (basket == null)
        {
            return new CheckoutBasketResult(false);
        }
        var eventMessage = command.BasketCheckoutDto.Adapt<BasketCheckoutEvent>();
        eventMessage.TotalPrice = basket.TotalPrice;
        await publishEndpoint.Publish(eventMessage, cancellationToken);
        await repository.DeleteBasket(command.BasketCheckoutDto.UserName, cancellationToken);
        
       return new CheckoutBasketResult(true);
    }
}