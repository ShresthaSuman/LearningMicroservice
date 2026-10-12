using MediatR;

namespace Ordering.Application.OrdersFeature.Commands.DeleteOrder;

public record DeleteOrderCommand(Guid OrderId) : ICommand<DeleteOrderResult>;
public record DeleteOrderResult(bool IsSuccess);

public class DelereOrderCommandValidation : AbstractValidator<DeleteOrderCommand>
{
    public DelereOrderCommandValidation()
    {
        RuleFor(x => x.OrderId).NotEmpty().WithMessage("OrderId cannot be empty");
    }
}