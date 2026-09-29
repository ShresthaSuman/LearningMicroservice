using Discount.gRPC.Model;
using Microsoft.EntityFrameworkCore;

namespace Discount.gRPC.Data;

public class DiscountContext :DbContext
{
    public DbSet<Coupon>? Coupons { get; set; }

    public DiscountContext(DbContextOptions<DiscountContext> options) :base(options)
    {
        
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Coupon>().HasData(
            new Coupon { Id = 1, ProductName = "IPhone X", Description = "Iphone X is a good phone", Amount = 100 },
            new Coupon { Id  = 2, ProductName = "IPhone 12", Description = "Iphone 12 is a good phone", Amount = 200 },
            new Coupon { Id = 3, ProductName = "IPhone 13", Description = "Iphone 13 is a good phone", Amount = 150 }

        );
    }
}