using Microsoft.EntityFrameworkCore;
using Ordering.Domain.Model;
using Ordering.Domain.ValueObjects;

namespace Ordering.Infrastructure.Data;

public class ApplicationDbContext :DbContext
{
  public  ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) :base(options)
    {
        
    }
  
  public DbSet<Customer>  Customers =>Set<Customer>();
  public DbSet<Order>  Orders =>Set<Order>();
  public DbSet<Product>  Products =>Set<Product>();
  public DbSet<OrderItem>  OrderItems =>Set<OrderItem>();
 //public DbSet<Address>   Addresses =>Set<Address>(); 
 
  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    modelBuilder.Entity<Customer>().Property(c => c.Name).IsRequired().HasMaxLength(100);
    base.OnModelCreating(modelBuilder);
  }
}

