using BuildingBlocks.CQRS;
using FluentValidation;
using Ordering.Application.Dtos;

namespace Ordering.Application.OrdersFeature.Commands;

public record CreateOrderCommand(OrderDto Order) : ICommand<CreateOrderResult>;

public record CreateOrderResult(Guid Id);

public class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderCommandValidator()
    {
        RuleFor(o => o.Order.OrderName).NotEmpty().WithMessage("Order name is required");
        RuleFor(o => o.Order.CustomerId).NotEmpty().WithMessage("Customer id is required");
        RuleFor(o=>o.Order.OrderItems).NotEmpty().WithMessage("Order items is required");
    }
}