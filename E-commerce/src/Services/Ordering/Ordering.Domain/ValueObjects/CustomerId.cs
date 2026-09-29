using Ordering.Domain.Exception;

namespace Ordering.Domain.ValueObjects;

public record CustomerId
{
    public Guid Value { get; }
    
   private CustomerId(Guid value) => Value = value;

   public static CustomerId Of(Guid value)
   {
       ArgumentNullException.ThrowIfNull(value);
       if (value == Guid.Empty)
       {
           throw new DomainException("CustomerId is empty");
       }

       return new CustomerId(value);
   }
    
}