using Carter;

namespace Ordering.API;

public static class DependencyInjection
{
    public static IServiceCollection AddAPIServices(this IServiceCollection services)
    {
        services.AddCarter();

        // services.AddExceptionHandler<CustomExceptionHandler>();
        // services.AddHealthChecks()
        //     .AddSqlServer(configuration.GetConnectionString("Database")!);

        return services;
    }

    public static WebApplication UseApiServices(this WebApplication app)
    {
        app.MapCarter();
        return app;
    }
}