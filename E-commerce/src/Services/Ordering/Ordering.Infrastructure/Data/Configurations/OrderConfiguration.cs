using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ordering.Domain.Enum;
using Ordering.Domain.Model;
using Ordering.Domain.ValueObjects;

namespace Ordering.Infrastructure.Data.Configurations;

public class OrderConfiguration :IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.HasKey(o=>o.Id);
        builder.Property(o=>o.Id).HasConversion(Oid =>Oid.Value ,dbId=> OrderId.Of(dbId) );
        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(o => o.CustomerId)
            .IsRequired();
           
        builder.HasMany<OrderItem>()
            .WithOne()
            .HasForeignKey(oi => oi.OrderId);
        builder.ComplexProperty(o=>o.OrderName, newBuilder =>
        {
            //orderItem
            newBuilder.Property(n => n.Value)
                .HasColumnName(nameof(Order.OrderName))
                .HasMaxLength(100)
                .IsRequired();
        });

        builder.ComplexProperty(o => o.BillingAddress, newBuilder =>
        {
            newBuilder.Property(a =>a.FirstName)
                .HasMaxLength(100)
                .IsRequired();
            newBuilder.Property(a =>a.LastName)
                .HasMaxLength(100)
                .IsRequired();

            newBuilder.Property(a => a.EmailAddress)
                .HasMaxLength(100)
                .IsRequired();
            newBuilder.Property(a=>a.AddressLine)
                .HasMaxLength(150)
                .IsRequired();
            newBuilder.Property(a => a.Country)
                .HasMaxLength(50)
                .IsRequired();

            newBuilder.Property(a => a.State)
                .HasMaxLength(50)
                .IsRequired();
            newBuilder.Property(a => a.ZipCode)
                .HasMaxLength(10)
                .IsRequired();
        });

        builder.ComplexProperty(o => o.ShippingAddress, newBuilder =>
        {
            newBuilder.Property(a =>a.FirstName)
                .HasMaxLength(100)
                .IsRequired();
            newBuilder.Property(a =>a.LastName)
                .HasMaxLength(100)
                .IsRequired();

            newBuilder.Property(a => a.EmailAddress)
                .HasMaxLength(100)
                .IsRequired();
            newBuilder.Property(a=>a.AddressLine)
                .HasMaxLength(150)
                .IsRequired();
            newBuilder.Property(a => a.Country)
                .HasMaxLength(50)
                .IsRequired();

            newBuilder.Property(a => a.State)
                .HasMaxLength(50)
                .IsRequired();
            newBuilder.Property(a => a.ZipCode)
                .HasMaxLength(10)
                .IsRequired();
        });

        builder.ComplexProperty(o => o.Payment, newBuilder =>
            {
                newBuilder.Property(p=>p.CardName)
                    .HasMaxLength(100)
                    .IsRequired();
                newBuilder.Property(p=>p.CardNumber)
                    .HasMaxLength(150)
                    .IsRequired();
                newBuilder.Property(p => p.Expiration)
                    .HasMaxLength(10);

                newBuilder.Property((p => p.CVV))
                    .HasMaxLength(3);
                  
                newBuilder.Property(p=>p.PaymentMethod)
                    .HasMaxLength(100)
                    .IsRequired();
            }
        );
        
        builder.Property(o =>o.Status)
            .HasDefaultValue(OrderStatus.Draft)
            .HasConversion(s => s.ToString(), s => (OrderStatus)Enum.Parse(typeof(OrderStatus),s));
        
        builder.Property(o => o.TotalPrice);
    }
}