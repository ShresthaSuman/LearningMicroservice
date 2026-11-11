namespace Shopping.Web.Models.Ordering;

public record AddressDtoModel(
    string FirstName, 
    string LastName, 
    string EmailAddress, 
    string AddressLine, 
    string Country, 
    string State, 
    string ZipCode
    );