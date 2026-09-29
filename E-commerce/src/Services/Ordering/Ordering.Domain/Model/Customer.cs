using Ordering.Domain.Abstraction;
using Ordering.Domain.ValueObjects;

namespace Ordering.Domain.Model;

public class  Customer : Entity<CustomerId>
{
    public string Name { get; private set; } = default!;
    public string Email { get; private set; } = default!;

    public Customer Create(CustomerId id, string name, string email)
    {
        var customer = new Customer
        {
            Id = id,
            Name = name,
            Email = email
        };
        return customer;
    }
}