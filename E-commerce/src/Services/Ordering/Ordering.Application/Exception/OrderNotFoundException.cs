using BuildingBlocks.Exceptions;

namespace Ordering.Application.Exception;

public class OrderNotFoundException : NotFoundException
{
    public OrderNotFoundException(Guid id) : base("order", id)
    {
    }

    public OrderNotFoundException(string name, object key) : base(name, key)
    {
    }
}