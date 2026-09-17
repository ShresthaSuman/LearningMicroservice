

using System.Net.Mime;
using Microsoft.AspNetCore.Diagnostics;

var builder = WebApplication.CreateBuilder(args);
var assembly = typeof(Program).Assembly;

builder.Services.AddCarter();

builder.Services.AddMediatR(cfg =>
{
    //builder.Services.AddMediatR(cfg => ...); uses cfg because MediatR must know
    //where your handlers are hidden to route your commands correctly.
    cfg.RegisterServicesFromAssembly(assembly);
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
});

builder.Services.AddMarten(opts =>
{
    opts.Connection(builder.Configuration.GetConnectionString("Database")!);
}).UseLightweightSessions();

builder.Services.AddValidatorsFromAssembly(assembly);

builder.Services.AddHealthChecks();

var app = builder.Build();

//app.MapGet("/", () => "Hello World!");
app.UseHealthChecks("/health");
app.MapCarter();

app.Run();