namespace Shopping.Web.Models.Ordering;

public record PaymentDtoModel(
    string CardName,  
    string CardNumber,
    string Expiration,
    string CVV,
    int PaymentMethod
    );