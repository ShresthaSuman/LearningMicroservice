using Ordering.API;
using Ordering.Application;
using Ordering.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplicationServices()
        .AddInfrastructureServices(builder.Configuration)
        .AddAPIServices();

var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.Run();