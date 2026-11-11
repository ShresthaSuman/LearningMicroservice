namespace Shopping.Web.Models.Ordering;

public record OrderItemDtoModel(
    Guid OrderId,
    Guid ProductId,
    int Quantity,
    decimal Price
    );