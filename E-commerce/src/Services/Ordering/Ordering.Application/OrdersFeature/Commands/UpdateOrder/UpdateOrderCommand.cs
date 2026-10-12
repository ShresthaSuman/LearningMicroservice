

namespace Ordering.Application.OrdersFeature.Commands.UpdateOrder;

public record UpdateOrderCommand(OrderDto Order) : ICommand<UpdateOrderResult>;
public record UpdateOrderResult(bool isSuccess);

public class UpdateOrderCommandValidator : AbstractValidator<UpdateOrderCommand>
{
    public UpdateOrderCommandValidator()
    {
        RuleFor(o=>o.Order.Id).NotEmpty().WithMessage("OrderId is required");
        RuleFor(o => o.Order.OrderName).NotEmpty().WithMessage("Order name is required");
        RuleFor(o=>o.Order.CustomerId).NotEmpty().WithMessage("CustomerId is required");
    }
}