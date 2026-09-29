# MediatR Logging, Validation & Exception Handling Notes

This reference guide summarizes how to implement a clean, maintainable architecture in ASP.NET Core using **MediatR Pipeline Behaviors**, **FluentValidation**, and the native **IExceptionHandler**.

---

## 1. Global Exception Handling Architecture

Instead of cluttering individual handlers with `try-catch` blocks, you can route all failures through a unified pipeline.

```
 [HTTP Request] ──> [MediatR Pipeline] ──> [Validation Behavior] ──> [Logging Behavior]
                                                                            │
 [HTTP Response] <── [IExceptionHandler] <── [Throws Exception] <───────────┘
```

### Components
* **MediatR Pipeline Behavior:** Intercepts requests globally before they hit the main domain logic.
* **FluentValidation:** Keeps business validation logic detached from entities.
* **IExceptionHandler:** A native ASP.NET Core middleware that catches unhandled exceptions globally and formats them as standard HTTP `ProblemDetails` (e.g., 400 Bad Request).

---

## 2. Code Implementation

### Step A: The Validation Pipeline Behavior
Intercepts requests to execute predefined rules before executing the handler.
```csharp
public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;
    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators) => _validators = validators;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (_validators.Any())
        {
            var context = new ValidationContext<TRequest>(request);
            var results = await Task.WhenAll(_validators.Select(v => v.ValidateAsync(context, cancellationToken)));
            var failures = results.SelectMany(r => r.Errors).Where(f => f != null).ToList();

            if (failures.Count != 0)
                throw new ValidationException(failures);
        }
        return await next();
    }
}
```

### Step B: The Global Exception Handler
Catches validation faults globally and reformats them cleanly for the frontend client.
```csharp
public class GlobalValidationExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not ValidationException validationException)
            return false;

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validation Failed",
            Detail = "One or more validation errors occurred."
        };
        
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true;
    }
}
```

### Step C: The Cross-Cutting Logging Behavior
Tracks request lifecycle execution metrics universally.
```csharp
public class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger) 
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        logger.LogInformation("Starting request {RequestName}", requestName);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var response = await next();
            stopwatch.Stop();
            logger.LogInformation("Handled request {RequestName} successfully in {ElapsedMilliseconds}ms", requestName, stopwatch.ElapsedMilliseconds);
            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger.LogError(ex, "Request {RequestName} failed after {ElapsedMilliseconds}ms", requestName, stopwatch.ElapsedMilliseconds);
            throw; // Propagates up to the global IExceptionHandler
        }
    }
}
```

### Step D: DI Registration (`Program.cs`)
```csharp
builder.Services.AddExceptionHandler<GlobalValidationExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

// Registration order determines pipeline execution order
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
```

---

## 3. Key Concepts Clarified

### Q: Why does the constructor use `ILogger<LoggingBehavior<TRequest, TResponse>>`?
This pattern is driven by **.NET Structured Logging** and **Generics**, not CQRS:
1. **Source Category Tagging:** `.NET` checks the class specified within `<...>` to automatically prepend log entries with a category string (`[YourNamespace.LoggingBehavior]`). This identifies *exactly* which class produced a log without manual string typing.
2. **Generic Matching:** Because `LoggingBehavior<TRequest, TResponse>` is a generic class dealing with variable request/response pairings, its true name changes dynamically at runtime (e.g., `LoggingBehavior<CreateOrderCommand, OrderResponse>`). The logger declaration must mirror this type precisely.

### Q: What is the purpose of MediatR's Pipeline Behavior?
It resolves code duplication by creating a centralized bottleneck/funnel for cross-cutting concerns:
* **Without it:** You would have to copy-paste logger invocations and validation logic into every distinct command/query handler file (e.g., across 50 separate classes).
* **With it:** You write the wrapper code **exactly once**. It intercepts all runtime executions, inspects metadata dynamically using reflection (like `typeof(TRequest).Name`), and streamlines structural application workflows cleanly.