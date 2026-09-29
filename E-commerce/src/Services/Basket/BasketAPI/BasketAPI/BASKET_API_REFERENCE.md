# Basket API Reference Guide

This note explains how the Basket API is organized, how each feature works, why each library is used, and what can be improved when starting another project.

## 1. What this service does

The Basket API stores one shopping cart per user. A cart contains:

- `UserName`: the cart owner and document identity.
- `Items`: products in the cart.
- `TotalPrice`: calculated from `Price * Quantity` for every item.

The service exposes three HTTP endpoints:

| Operation | Method and route | Purpose |
|---|---|---|
| Store basket | `POST /basket` | Applies discounts and saves a cart |
| Get basket | `GET /basket/{UserName}` | Reads a cart by user name |
| Delete basket | `DELETE /basket/{UserName}` | Deletes a cart |

The project uses a feature-oriented structure instead of putting all endpoints and logic in one file.

## 2. Project structure

```text
BasketAPI/
├── Data/
│   ├── IBasketRepository.cs          # Persistence abstraction
│   ├── BasketRepository.cs           # Marten/PostgreSQL implementation
│   └── CachedBasketRepository.cs     # Redis decorator around the repository
├── Features/
│   ├── StoreBasket/
│   ├── GetBasket/
│   └── DeleteBasket/
├── Models/
│   ├── ShoppingCart.cs
│   └── ShoppingCartItem.cs
├── Exception/
│   └── BasketNotFoundException.cs
├── Protos/
│   └── discount.proto                # gRPC contract used by the client
├── Program.cs                         # Dependency injection and middleware setup
├── GlobalUsing.cs                     # Shared using directives
├── appsettings.json                   # Local configuration
├── Dockerfile                         # Container image build
└── BasketAPI.csproj                   # Packages and generated gRPC client setup
```

A useful feature-folder pattern is:

```text
FeatureName/
├── FeatureEndpoint.cs
├── FeatureHandler.cs
└── optional request, response, command, query, and validator types
```

This keeps transport code, application logic, and validation close together.

## 3. Step-by-step implementation flow

### Step 1: Define the domain model

`ShoppingCart` contains the user name and item list. `ShoppingCartItem` contains product information, quantity, color, price, and product ID.

`ShoppingCart.TotalPrice` is a computed property:

```csharp
public decimal TotalPrice => Items.Sum(x => x.Price * x.Quantity);
```

Why this is useful:

- The total is always derived from the current items.
- It avoids storing a value that could become stale.
- It keeps the calculation close to the model.

Improvement:

- Use required properties or constructor validation instead of `default!` for required values.
- Validate positive quantity and non-negative price.
- Consider a value object or decimal-money rules if currency handling becomes more complex.

### Step 2: Create a persistence abstraction

`IBasketRepository` defines the operations needed by the application:

```csharp
Task<ShoppingCart> GetBasket(string username, CancellationToken cancellationToken = default);
Task<ShoppingCart> StoreBasket(ShoppingCart basket, CancellationToken cancellationToken = default);
Task<bool> DeleteBasket(string username, CancellationToken cancellationToken = default);
```

Why use an interface:

- Handlers do not depend directly on Marten.
- The implementation can be replaced in tests.
- Caching can be added without changing the handlers.

Improvement:

- Use a result such as `ShoppingCart?` or a typed result for a missing basket instead of relying only on an exception.
- Keep naming consistent: `username` could be `userName` everywhere.

### Step 3: Store documents with Marten

`BasketRepository` uses `IDocumentSession`:

- `LoadAsync<ShoppingCart>(username)` loads by the configured identity.
- `session.Store(basket)` schedules an insert or update.
- `session.Delete<ShoppingCart>(username)` schedules deletion.
- `SaveChangesAsync` commits the transaction.

Registration in `Program.cs`:

```csharp
builder.Services.AddMarten(opts =>
{
    opts.Connection(builder.Configuration.GetConnectionString("Database")!);
    opts.Schema.For<ShoppingCart>().Identity(x => x.UserName);
}).UseLightweightSessions();
```

Why Marten:

- It stores .NET documents in PostgreSQL.
- It provides document-style persistence while using PostgreSQL as the database.
- User name is configured as the document identity, matching the basket lookup route.

Improvement:

- Use explicit schema/database migration management for production.
- Add indexes if queries expand beyond identity lookup.
- Do not expose database credentials in committed configuration.
- Consider optimistic concurrency when multiple requests can update the same cart.

### Step 4: Add Redis caching with the decorator pattern

`CachedBasketRepository` implements the same `IBasketRepository` interface and wraps the real repository:

```csharp
builder.Services.AddScoped<IBasketRepository, BasketRepository>();
builder.Services.Decorate<IBasketRepository, CachedBasketRepository>();
```

This creates the following chain:

```text
Handler -> CachedBasketRepository -> BasketRepository -> PostgreSQL
                         |
                         └── Redis
```

#### Read flow

1. Look for a serialized basket in Redis using the user name as the key.
2. If found, deserialize and return it.
3. If not found, read from PostgreSQL.
4. Serialize the result and put it in Redis.
5. Return the basket.

#### Write flow

1. Save the basket to PostgreSQL.
2. Update the Redis entry with the saved basket.
3. Return the basket.

#### Delete flow

1. Delete the basket from PostgreSQL.
2. Remove the Redis entry.
3. Return success.

Why this pattern is useful:

- The handler remains unaware of caching.
- Cache behavior can be changed independently.
- The database remains the source of truth.
- The cache is updated after writes and invalidated after deletes.

Current cache key:

```text
username
```

Improvement:

- Prefix keys, for example `basket:{username}`, to avoid collisions with other features.
- Add an expiration time using `DistributedCacheEntryOptions`; otherwise stale keys can remain indefinitely.
- Handle cache outages deliberately. A cache failure should usually fall back to the database rather than break the request.
- Configure serializer options explicitly and consider versioning cached data.
- Protect against cache stampedes when many requests miss the same key.
- Consider cancellation in `GetBasketHandler`; it currently does not pass its cancellation token to the repository call.
- Consider invalidating the cache before or after a write according to the consistency guarantees required by the application.

Redis registration:

```csharp
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
});
```

## 4. CQRS and MediatR request flow

The project separates commands that change state from queries that read state.

### Store basket command

`StoreBasketCommand` contains a `ShoppingCart` and returns `StoreBasketResult`.

Flow:

```text
POST /basket
  -> StoreBasketEndpoint
  -> StoreBasketCommand
  -> ValidationBehavior
  -> LoggingBehavior
  -> StoreBasketHandler
  -> Discount gRPC call for every item
  -> CachedBasketRepository.StoreBasket
  -> PostgreSQL and Redis
```

Before saving, `StoreBasketHandler` calls the generated Discount gRPC client:

```csharp
var coupon = await discountProtoServiceClient.GetDiscountAsync(
    new GetDiscountRequest { ProductName = item.ProductName },
    cancellationToken: cancellationToken);

item.Price -= coupon.Amount;
```

Improvement:

- Prevent the final price from becoming negative with `Math.Max(0, item.Price - coupon.Amount)`.
- Make discount application idempotent. Re-saving the same cart currently subtracts the discount again from the already discounted price.
- Prefer storing the original price and calculating the discounted price separately.
- Decide whether discount-service failure should reject the whole basket or save without a discount.
- Use a timeout/retry/circuit-breaker policy for gRPC calls.
- Consider calling the Discount service in parallel for independent items, with a controlled limit.

### Get basket query

`GetBasketQuery` contains the user name and returns `GetBasketResult`.

Flow:

```text
GET /basket/{UserName}
  -> GetBasketEndpoint
  -> GetBasketQuery
  -> GetBasketHandler
  -> CachedBasketRepository.GetBasket
  -> Redis, then PostgreSQL on cache miss
```

If the basket is not found, `BasketRepository` throws `BasketNotFoundException`.

### Delete basket command

`DeleteBasketCommand` contains the user name and returns a success flag.

Flow:

```text
DELETE /basket/{UserName}
  -> DeleteBasketEndpoint
  -> DeleteBasketCommand
  -> DeleteBasketHandler
  -> CachedBasketRepository.DeleteBasket
  -> PostgreSQL delete and Redis invalidation
```

## 5. Validation and pipeline behaviors

The project registers MediatR pipeline behaviors:

```csharp
config.AddOpenBehavior(typeof(ValidationBehavior<,>));
config.AddOpenBehavior(typeof(LoggingBehavior<,>));
```

### FluentValidation

Current validation includes:

- Store basket must contain a cart.
- Store basket user name must not be empty.
- Delete basket user name must not be empty.

Why validation belongs before handlers:

- Invalid input is rejected consistently.
- Handlers focus on business logic.
- Rules are easy to test independently.

Improvement:

- Validate item count, product ID, product name, quantity, color, and price.
- Add validation for maximum basket size.
- Return a consistent problem-details response for validation failures.
- Add validators for every command and query that accepts user input.

### LoggingBehavior

The shared behavior logs:

- Request start.
- Successful completion and elapsed milliseconds.
- A warning when execution exceeds 3000 ms.
- Exceptions and elapsed time.

Why this is useful:

- It gives a common performance signal for every MediatR request.
- It avoids repeating timing code in every handler.

Improvement:

- Correct the log label typo `PERMORFANCE`.
- Add correlation/trace IDs and user identifiers where safe.
- Avoid logging sensitive basket contents.
- Use structured metrics and tracing for production performance analysis.

## 6. Discount gRPC client integration

The Basket project generates a client from the Discount service contract:

```xml
<Protobuf Include="..\..\..\Discount\Discount.gRPC\Protos\discount.proto" GrpcServices="Client">
    <Link>Protos\discount.proto</Link>
</Protobuf>
```

The client is registered in `Program.cs`:

```csharp
builder.Services.AddGrpcClient<DiscountProtoService.DiscountProtoServiceClient>(options =>
{
    options.Address = new Uri(builder.Configuration["GrpcSettings:DiscountUrl"]!);
});
```

Current Docker configuration points Basket to:

```text
https://discount-gRPC:8081
```

Important Docker rule:

- `localhost` inside Basket means the Basket container itself.
- `discount-gRPC` is the Docker Compose service name.
- `8081` is the Discount container port.
- `6062` is the host-published port and is not used for container-to-container calls.

For local development, the client currently bypasses server certificate validation only when the environment is Development:

```csharp
if (builder.Environment.IsDevelopment())
{
    handler.ServerCertificateCustomValidationCallback =
        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
}
```

This is acceptable only as a temporary local-development workaround. It must not be used in production.

Improvement:

- Use a certificate whose SAN includes `discount-gRPC`.
- Install/trust the issuing CA in the Basket container.
- Remove the bypass before production deployment.
- Validate the configured URL at startup and fail with a clear configuration message if it is missing.
- Add gRPC deadlines and resilience policies.

## 7. HTTP endpoints and Carter

Carter is registered with:

```csharp
builder.Services.AddCarter();
app.MapCarter();
```

Each endpoint implements `ICarterModule` and registers routes in its `AddRoutes` method.

The endpoints use Mapster for mapping request/command and result/response objects:

```csharp
var command = request.Adapt<StoreBasketCommand>();
var response = result.Adapt<StoreBasketResponse>();
```

Why Carter:

- It keeps minimal API route definitions organized in separate modules.
- It avoids a large `Program.cs` file.
- It works well with feature folders.

Why Mapster:

- It reduces repetitive object-to-object mapping code.
- It supports concise request and response transformations.

Improvement:

- Use explicit mappings at important service boundaries when names or types differ.
- Document endpoint response status codes accurately. `POST /basket` returns `201 Created`, but the endpoint currently declares `Produces(200)`.
- Use route parameter naming conventions consistently, for example `userName` rather than `UserName`.
- Add OpenAPI/Swagger support if the API will be consumed by other teams.

## 8. CORS configuration

There is currently **no CORS policy configured** in the Basket project.

A search found no `AddCors`, `UseCors`, or endpoint CORS policy. CORS is only required when a browser frontend hosted at a different origin calls the Basket HTTP API. It is not required for server-to-server calls from Basket to Discount over gRPC.

If a browser frontend is added, configure a named policy and allow only known origins:

```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy.WithOrigins("https://frontend.example.com")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

app.UseCors("Frontend");
```

Do not use `AllowAnyOrigin()` together with credentials. In production, avoid allowing every origin.

## 9. Health checks

The project registers health checks for:

- Basket PostgreSQL database.
- Redis.

The endpoint is:

```text
GET /health
```

The UI response writer is used so health results are returned in a standard health-check format.

Improvement:

- Separate liveness and readiness endpoints.
- Include the Discount gRPC dependency in readiness checks if Basket cannot work without it.
- Do not expose detailed internal dependency information publicly without authorization.
- Configure health-check timeouts.

## 10. Error handling

The project defines `BasketNotFoundException`, but `Program.cs` currently uses:

```csharp
app.UseExceptionHandler(option => { });
```

An empty exception-handler configuration can produce confusing 404 errors when an exception occurs. For example, a Discount gRPC connection failure may be wrapped by an exception-handler 404 message.

Improvement:

- Add a global exception handler that returns `ProblemDetails`.
- Map `BasketNotFoundException` to HTTP 404.
- Map validation failures to HTTP 400.
- Map downstream gRPC unavailability to an appropriate 503 response.
- Log the original exception while returning a safe client response.
- Avoid exposing stack traces outside Development.

## 11. Configuration and environments

Current local settings include:

```json
"ConnectionStrings": {
  "Database": "Host=localhost;Port=5433;Database=BasketDb;...",
  "Redis": "localhost:6379"
},
"GrpcSettings": {
  "DiscountUrl": "https://localhost:5052"
}
```

Docker overrides use environment variables:

```yaml
ConnectionStrings__Database: Host=basket-db;Port=5432;Database=BasketDb;...
ConnectionStrings__Redis: distributedcache-redis:6379
GrpcSettings__DiscountUrl: https://discount-gRPC:8081
```

The double underscore maps to nested configuration keys. For example:

```text
GrpcSettings__DiscountUrl
```

maps to:

```text
GrpcSettings:DiscountUrl
```

Improvement:

- Keep secrets out of source-controlled JSON and Compose files.
- Use user secrets locally and a secret manager in deployed environments.
- Use typed options classes such as `IOptions<GrpcSettings>` instead of string-based configuration lookups.
- Validate required configuration on startup.
- Keep Development and Production settings separate.

## 12. Docker and Compose setup

The Basket image uses a multi-stage Dockerfile:

1. Start from the ASP.NET runtime image.
2. Use the SDK image to restore packages.
3. Build the application.
4. Publish the application.
5. Copy only published output into the runtime image.

This reduces the final image size and keeps the SDK out of the runtime container.

Compose provides:

- `basket-db`: PostgreSQL for Basket.
- `distributedcache-redis`: Redis.
- `basket-api`: Basket service.
- `discount-gRPC`: Discount service.

The `postgres_basket` named volume preserves Basket database data when the container is deleted. Removing the volume permanently deletes that data.

Useful commands:

```bash
# Start Basket and dependencies
docker compose up -d --build basket-api

# Check service state
docker compose ps

# Read Basket logs
docker compose logs -f basket-api

# Check effective configuration
docker compose config

# Recreate after Compose changes
docker compose up -d --build --force-recreate basket-api

# Stop/remove only the Basket container
docker compose rm -sf basket-api
```

Be careful with:

```bash
docker compose down -v
```

This removes Compose volumes and can delete PostgreSQL data.

## 13. Libraries used

| Library | Usage in Basket API |
|---|---|
| Carter | Organizes minimal API endpoint modules |
| MediatR | Sends commands/queries and runs pipeline behaviors |
| BuildingBlocks | Shared CQRS interfaces and validation/logging behaviors |
| FluentValidation | Validates commands before handlers execute |
| Marten | Stores shopping carts as PostgreSQL documents |
| Npgsql health checks | Checks Basket PostgreSQL availability |
| StackExchange.Redis cache integration | Registers Redis as `IDistributedCache` |
| Redis health checks | Checks Redis availability |
| Microsoft.Extensions.Caching.StackExchangeRedis | Redis distributed cache provider |
| Scrutor | Decorates `IBasketRepository` with `CachedBasketRepository` |
| Mapster | Maps request/command and result/response objects |
| Grpc.AspNetCore | Generates and registers gRPC client support |
| Google.Protobuf / generated code | Represents the Discount gRPC contract |

## 14. Recommended next improvements

1. Add automated unit tests for each handler, validator, repository decorator, and endpoint.
2. Add integration tests using test containers for PostgreSQL, Redis, and Discount gRPC.
3. Make discount calculation idempotent and protect against negative prices.
4. Add cache expiration, key prefixes, and cache-failure fallback behavior.
5. Replace the empty exception handler with consistent ProblemDetails responses.
6. Add typed options and startup configuration validation.
7. Remove the development-only certificate bypass before production.
8. Add CORS only when a browser frontend requires it, with an allow-list of trusted origins.
9. Add authentication and authorization before exposing baskets in a real application.
10. Add observability: correlation IDs, OpenTelemetry tracing, metrics, and structured logs.
11. Align declared response metadata with actual status codes, especially `POST /basket` returning `201 Created`.
12. Pass cancellation tokens consistently through every repository call.
13. Add request limits and validation for basket size, quantity, price, and product identifiers.
14. Use retry, timeout, and circuit-breaker policies for downstream Discount calls.

## 15. A reusable implementation checklist

When creating a similar service:

- [ ] Define the model and its invariants.
- [ ] Create an interface for persistence.
- [ ] Implement the repository and cancellation support.
- [ ] Add a cache decorator with clear key and expiration rules.
- [ ] Create command/query records and handlers.
- [ ] Add FluentValidation rules.
- [ ] Register MediatR pipeline behaviors.
- [ ] Add feature-specific endpoint modules.
- [ ] Add downstream clients from a versioned contract such as protobuf.
- [ ] Configure local, container, and production URLs separately.
- [ ] Add health checks for required dependencies.
- [ ] Add consistent exception-to-HTTP mapping.
- [ ] Add CORS only for browser cross-origin requirements.
- [ ] Protect secrets and certificates.
- [ ] Add unit and integration tests before expanding functionality.
- [ ] Document Docker volumes and data-loss commands.
