
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ordering.Infrastructure.Data.Extensions;

/// <summary>
/// Contains startup helpers for preparing the Ordering service's database.
/// </summary>
/// <remarks>
/// This is a static class because C# extension methods must be declared in a
/// top-level static class. The API can call its methods as extensions on the
/// built <see cref="WebApplication"/>.
/// </remarks>
public static class DatabaseExtensions
{
    /// <summary>
    /// Applies any pending Entity Framework Core migrations to the Ordering database.
    /// </summary>
    /// <param name="app">
    /// The built application, used to access the dependency injection container.
    /// </param>
    /// <returns>A task that completes after migrations have been applied.</returns>
    /// <remarks>
    /// The method creates a dependency injection scope because
    /// <see cref="ApplicationDbContext"/> is registered as a scoped service.
    /// It resolves that context and calls <c>Database.MigrateAsync()</c>, which
    /// creates the database if necessary, applies migrations not yet recorded in
    /// <c>__EFMigrationsHistory</c>, and leaves already-applied migrations alone.
    ///
    /// Calling this after building the app and before <c>Run</c> ensures the
    /// schema is up to date before the service handles requests. This is useful
    /// in development and simple deployments. In production, migrations are
    /// often run as a reviewed deployment step instead, especially when several
    /// application instances might start at once or the runtime account should
    /// not have schema-changing permissions.
    ///
    /// Migration errors are intentionally not suppressed: if the schema cannot
    /// be updated, startup fails instead of serving requests against an
    /// incompatible database. This method applies schema migrations only; it
    /// does not seed application data.
    /// </remarks>
    public static async Task InitialiseDatabaseAsync(this WebApplication app )
    {  
        using var scope = app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.Database.MigrateAsync().GetAwaiter().GetResult();
        await SeedAsync(context);
    }
    
    private static async Task SeedAsync(ApplicationDbContext context)
    {
        await SeedCustomerAsync(context);
        await SeedProductAsync(context);
        await SeedOrderWithItemAsync(context);
       

    }

    private static async Task SeedCustomerAsync(ApplicationDbContext context)
    {
        if (!await context.Customers.AnyAsync())
        {
            await context.Customers.AddRangeAsync(InitialData.Customers);
            await context.SaveChangesAsync();
        }
        
    }
    private static async Task SeedProductAsync(ApplicationDbContext context)
    {
        if (!await context.Products.AnyAsync())
        {
            await context.Products.AddRangeAsync(InitialData.Products);
            await context.SaveChangesAsync();
        }
        
    }
    private static async Task SeedOrderWithItemAsync(ApplicationDbContext context)
    {
        if (!await context.Orders.AnyAsync())
        {
            await context.Orders.AddRangeAsync(InitialData.OrdersWithItems);
            await context.SaveChangesAsync();
        }
    }
}