using Carter;
using Marten;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCarter();
builder.Services.AddMediatR(cfg =>
{
    //builder.Services.AddMediatR(cfg => ...); uses cfg because MediatR must know
    //where your handlers are hidden to route your commands correctly.
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
});

builder.Services.AddMarten(opts =>
{
    opts.Connection(builder.Configuration.GetConnectionString("Database")!);
}).UseLightweightSessions();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyHeader()
            .WithMethods("GET", "POST", "PUT", "DELETE"); // Make sure PUT is here!
    });
});

var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.MapCarter();
app.Run();