# Ordering Service: Step-by-Step Technical Guide

This guide explains how the Ordering service is structured, how an order travels from an HTTP request to SQL Server and back, why the main design choices are used, how to run and extend the service, and which limitations in the current implementation should be understood before production use.

The guide describes the implementation in `Services/Ordering` as it exists in this repository. Where the current code has a limitation or a likely improvement, it is identified explicitly rather than presented as behavior the service already guarantees.

## 1. What the Ordering service does

Ordering is one independently organized service in the E-commerce solution. Its API accepts order commands and queries, its domain project defines order-related business concepts, and its infrastructure project persists those concepts using Entity Framework Core (EF Core) and SQL Server.

The main responsibilities are:

- Create an order with customer, address, payment, and item data.
- Update an existing order's name, addresses, payment data, and status.
- Delete an order.
- List orders with pagination.
- Find orders by name or customer.
- Persist orders and their items in SQL Server.
- Raise and dispatch domain events for order creation and update.

An order is not just a row. It is modeled as an **aggregate**: the `Order` root owns its collection of `OrderItem` entities and is the main object through which order changes are made.

## 2. Project layout and dependency direction

The service is divided into four projects:

| Project | Responsibility | Typical contents |
| --- | --- | --- |
| `Ordering.Domain` | Business model and rules, independent of HTTP and EF Core | `Order`, `OrderItem`, value objects, status, domain events |
| `Ordering.Application` | Use cases and application orchestration | Commands, queries, handlers, validators, DTOs |
| `Ordering.Infrastructure` | Technical implementation of persistence and database behavior | `ApplicationDbContext`, EF mappings, SQL Server registration, migrations and interceptors |
| `Ordering.API` | HTTP interface and application startup | Carter endpoints, dependency registration, `Program.cs` |

The intended dependency direction is:

```text
Ordering.API ───────> Ordering.Application <────── Ordering.Infrastructure
                              │                              │
                              └────────> Ordering.Domain <───┘
```

The domain should not depend on the API or database. The application layer depends on domain types and an application-owned persistence abstraction, `IApplicationDbContext`. Infrastructure implements that abstraction. This keeps the use-case code from having to construct a SQL Server context directly.

Important folders:

```text
Services/Ordering/
  Ordering.API/Endpoints/
  Ordering.Application/OrdersFeature/Commands/
  Ordering.Application/OrdersFeature/Queries/
  Ordering.Application/OrdersFeature/EventHandlers/
  Ordering.Application/Dtos/
  Ordering.Domain/Model/
  Ordering.Domain/ValueObjects/
  Ordering.Domain/Events/
  Ordering.Domain/Abstraction/
  Ordering.Infrastructure/Data/Configurations/
  Ordering.Infrastructure/Data/Interceptors/
  Ordering.Infrastructure/Data/Migrations/
```

## 3. The request-to-database flow

For a typical write request, the sequence is:

1. A client sends HTTP JSON to an endpoint in `Ordering.API`.
2. Carter binds the request to an endpoint request type (or to individual route/query parameters).
3. Mapster maps the API request to an application command.
4. The endpoint sends that command through MediatR's `ISender`.
5. MediatR runs configured pipeline behaviors, including FluentValidation validation for commands.
6. The matching application handler performs the use case. It turns primitive input into domain value objects and invokes behavior on the `Order` aggregate.
7. The handler changes data through `IApplicationDbContext` and calls `SaveChangeAsync`.
8. EF Core tracks the changes. Registered interceptors update audit fields and dispatch any pending domain events during save.
9. EF Core writes the mapped values to SQL Server.
10. The handler returns an application result; Mapster maps it to an API response; the endpoint returns an HTTP result.

For a read request, the endpoint sends a query through MediatR. A query handler reads using EF Core, commonly with `AsNoTracking()`, maps domain objects to DTOs, and returns the query result. The endpoint maps that result to its response type.

This separation is useful because HTTP binding, business rules, persistence, and response formatting can evolve for different reasons. It also makes the handler and domain behavior easier to test without hosting the API.

## 4. Domain model: identity, aggregate, and value objects

### 4.1 Entities and aggregate roots

`Entity<T>` in `Ordering.Domain/Abstraction/Entity.cs` supplies a typed `Id` and audit properties. `Aggregate<TId>` extends it with a collection of pending domain events and a method to clear them after dispatch.

`Order : Aggregate<OrderId>` is the aggregate root. It contains:

- `OrderId` identity.
- `CustomerId` reference.
- `OrderName`.
- Shipping and billing addresses.
- Payment information.
- `OrderStatus`.
- A private list of `OrderItem` objects, exposed as a read-only list.

`OrderItem : Entity<OrderItemId>` records the order ID, product ID, quantity, and price. Its constructor is internal, so normal application code adds items through `Order.Add(...)`, not by freely constructing items outside the domain assembly.

The aggregate boundary gives one place to enforce rules for changes to an order. For example, `Order.Add` rejects a quantity or price that is zero or negative. `TotalPrice` is calculated from item price times quantity rather than stored as a separately maintained number.

### 4.2 Value objects and the `Of` factory

The ID types (`OrderId`, `CustomerId`, `ProductId`, `OrderItemId`) and concepts such as `Address`, `OrderName`, and `Payment` are records in `Ordering.Domain/ValueObjects`.

For types with validation, the constructor is private and a public static factory such as `OrderId.Of(value)` is used:

```csharp
var id = OrderId.Of(guid);
```

The factory does not omit object creation. It checks the supplied value and then invokes the private constructor. In the `OrderId` case, this prevents an `OrderId` containing `Guid.Empty` from being created through ordinary application code. `static` is appropriate because the factory does not need an existing `OrderId` instance.

This factory pattern is useful when the type has an invariant to enforce. It is not a DDD requirement for every value object: a type with no construction rules can use a simpler constructor. The important goal is to keep the value immutable and prevent invalid states where the domain has meaningful rules.

Current validation examples:

- `OrderId.Of`, `CustomerId.Of`, and the other ID factories reject `Guid.Empty`.
- `OrderName.Of` rejects a blank name and requires exactly five characters. Seed names such as `ORD_1` meet that rule.
- `Address.Of` currently requires a non-empty email address and address line.
- `Payment.Of` currently requires card name, card number, and CVV, and limits CVV length to at most three characters.
- `Order.Add` requires positive quantity and price.

These rules are what the code currently checks; they are not a complete validation policy. For example, address names, country, state, and postal code are not all checked in the factory, and an email format or expiration date is not validated there.

### 4.3 Domain events

`Order.Create(...)` adds an `OrderCreatedEvent`. `Order.Update(...)` adds an `OrderUpdatedEvent`. The event handlers in the application project currently log the event type; they do not yet publish to a message broker or trigger another service.

Domain events are useful for expressing that something meaningful happened in the domain without making the domain call infrastructure directly. The interceptor in this implementation publishes them through MediatR as part of EF's save process. The timing and reliability implications of that implementation are discussed in section 10.

## 5. Application layer: commands, queries, and validation

### 5.1 Commands change state

Commands express an intent to change data. The current write commands are:

| Command | Handler behavior |
| --- | --- |
| `CreateOrderCommand` | Builds domain values and an `Order`, adds its items, adds it to the context, and saves |
| `UpdateOrderCommand` | Loads the order by ID, replaces selected values, calls `Order.Update`, and saves |
| `DeleteOrderCommand` | Loads the order by ID, removes it, and saves |

Each handler takes a cancellation token and passes it to database calls. This lets an HTTP cancellation or request timeout propagate down to EF Core.

`CreateOrderCommandValidator`, `UpdateOrderCommandValidator`, and `DelereOrderCommandValidation` use FluentValidation. The validator checks basic request conditions, such as requiring an order name and customer ID. Application services register MediatR and the `ValidationBehavior<,>` pipeline behavior, which runs validators before a command handler. A failed validation raises a `ValidationException`.

Validation has two useful layers:

- **Request validation** checks input shape and use-case requirements, such as required fields.
- **Domain validation** protects business invariants regardless of which caller creates or changes the domain object.

Keeping important invariants in the domain prevents a different entry point from accidentally bypassing them. Request validation can still return clearer, aggregated errors before domain construction.

### 5.2 Queries read state

The current query handlers support:

- Paginated listing in `GetOrderQueryHandler`.
- Partial name matching in `GetOrderByNameQueryHandler`.
- Customer filtering in `GetOrderByCustomerHandler`.

The handlers use `AsNoTracking()` for read-only work to avoid EF change-tracking overhead. They include order items because those are needed to construct the returned `OrderDto`. The pagination handler counts rows, sorts by order name, then applies `Skip` and `Take`.

`PaginationRequest` is defined in BuildingBlocks with defaults of `pageIndex = 1` and `pageSize = 10`. The `GetOrder` endpoint uses `[AsParameters]`, which tells ASP.NET Core minimal APIs to bind the properties from individual HTTP parameters (for example, `?pageIndex=1&pageSize=10`) rather than treating the complex object as a JSON request body. `PaginationResult<T>` returns the page index, page size, total count, and page data.

### 5.3 DTOs separate the API shape from the domain

`OrderDto`, `AddressDto`, `PaymentDto`, and `OrderItemDto` are application transport shapes. The domain uses typed IDs and value objects; the DTOs use simple values such as `Guid`, `string`, and `decimal`, which are easier to serialize as JSON.

`OrderExtension.OrdersToOrderDtoList()` converts domain orders to DTOs for query responses. Mapster is used at the API boundary to map request/result records to other records. These are different mapping jobs:

- Domain-to-DTO mapping explicitly unwraps value objects and builds nested DTOs.
- API/application mapping adapts records that have compatible member names and types.

If a type's members do not match, prefer an explicit mapping configuration or constructor call over assuming a mapper can infer the intended semantics.

## 6. HTTP endpoints currently exposed

The endpoints are Carter modules under `Ordering.API/Endpoints`:

| HTTP method and route | Purpose | Main input |
| --- | --- | --- |
| `POST /orders` | Create an order | JSON `CreateOrderRequest` containing an `order` |
| `PUT /orders` | Update an order | JSON `UpdateOrderRequest` containing an order, including its ID |
| `DELETE /orders/{id}` | Delete by order ID | GUID route value |
| `GET /orders/` | List orders | Query parameters for pagination |
| `GET /orders/{orderName}` | Search by order name | Name route value |
| `GET /orders/customer/{customerId}` | Find a customer's orders | Customer GUID route value |

The API request records and response records are endpoint-specific. For example, create returns a response with an order ID and uses HTTP `201 Created`; update and delete currently return success records with HTTP `200 OK`; read operations return `200 OK`.

Example request shapes:

```http
GET /orders/?pageIndex=1&pageSize=10
GET /orders/ORD_1
GET /orders/customer/58c49479-ec65-4de2-86e7-033c546291aa
DELETE /orders/00000000-0000-0000-0000-000000000001
```

The create and update request records wrap the order DTO in an `order` property, so their JSON body is structurally like this (use mock payment data only for local learning):

```json
{
  "order": {
    "id": "00000000-0000-0000-0000-000000000000",
    "customerId": "58c49479-ec65-4de2-86e7-033c546291aa",
    "orderName": "ORD_1",
    "shippingAddress": {
      "firstName": "Test",
      "lastName": "Customer",
      "emailAddress": "test@example.invalid",
      "addressLine": "1 Example Street",
      "country": "Example",
      "state": "Example",
      "zipCode": "00000"
    },
    "billingAddress": {
      "firstName": "Test",
      "lastName": "Customer",
      "emailAddress": "test@example.invalid",
      "addressLine": "1 Example Street",
      "country": "Example",
      "state": "Example",
      "zipCode": "00000"
    },
    "payment": {
      "cardName": "Test only",
      "cardNumber": "mock-value",
      "expiration": "12/30",
      "cvv": "000",
      "paymentMethod": 1
    },
    "status": 2,
    "orderItems": [
      {
        "orderId": "00000000-0000-0000-0000-000000000000",
        "productId": "5334c996-8457-4cf0-815c-ed2b77c4ff61",
        "quantity": 1,
        "price": 1
      }
    ]
  }
}
```

For creation, the API generates the new order ID in the application handler; the sample's empty `id` and item `orderId` are only placeholders to show the DTO shape, not values to rely on. The customer and product IDs must refer to records accepted by the current database constraints. Update uses the existing order ID. The price shown is only a mock value: the current handler trusts the request's item price, which is a limitation, not a recommended pricing design.

There is a significant implementation issue to address in `Endpoints/CreateOrder.cs`: the endpoint currently calls `sender.Send(command)` without `await`, then tries to adapt the returned `Task` as if it were the command result. Since `Send` is asynchronous, this should be awaited before mapping the result:

```csharp
var result = await sender.Send(command);
var response = result.Adapt<CreateOrderResponse>();
```

Until that is corrected, successful creation may not produce the intended response and may fail when mapping. The intended request path above describes the design; this particular endpoint is not currently following it correctly.

## 7. Persistence and EF Core mapping

`ApplicationDbContext` in Infrastructure implements `IApplicationDbContext` and exposes `DbSet`s for orders, order items, customers, and products. It applies all `IEntityTypeConfiguration` mappings in its assembly.

The entity configurations define how the domain model is stored:

- `OrderConfiguration` maps the order key and converts `OrderId` between its domain wrapper and a SQL `Guid`.
- `CustomerId` is configured as an order foreign key; `OrderItem` rows are related to the order.
- `OrderName`, addresses, and payment use EF Core complex-property mappings so their fields are stored with the owning order instead of requiring separate aggregate tables.
- `OrderItemConfiguration` converts `OrderItemId` to/from a database `Guid`, maps product relationships, and requires quantity and price.
- `CustomerConfiguration` converts `CustomerId`, constrains name/email lengths, and creates a unique email index.

The database stores primitive columns such as GUIDs and strings; application/domain code uses the richer typed values. EF Core value conversions bridge the two representations.

The infrastructure dependency injection method reads the `ConnectionStrings:Database` setting, configures SQL Server, and registers both save interceptors. The current API settings name the database `OrderDb`; Compose contains an SQL Server container called `order-db` with host port `1433`.

### Migrations and development seed data

Migrations are in `Ordering.Infrastructure/Data/Migrations`. A migration records a versioned schema change, allowing EF Core to build or update a database without manually creating every table.

`Program.cs` calls `InitialiseDatabaseAsync()` only in the Development environment. That startup helper migrates the database and seeds customers, products, and sample orders if the corresponding tables are empty. Seed orders refer to the seeded customer and product IDs.

The migration step and seed step are separate concerns:

- Migration changes the schema.
- Seeding adds initial data.

The current startup helper uses `MigrateAsync().GetAwaiter().GetResult()` inside an asynchronous method. A straightforward improvement is to `await context.Database.MigrateAsync()` instead. Production systems often apply migrations as a controlled deployment action rather than having every application instance alter the schema at startup.

## 8. Run the Ordering service locally

The API launch profile uses `http://localhost:5003` and `https://localhost:5053`. The service expects a reachable SQL Server using the `ConnectionStrings:Database` setting in `Ordering.API/appsettings.json` or an overriding configuration source.

1. Start the `order-db` service from the repository's `E-commerce/src/compose.yaml`, or start another compatible SQL Server instance.
2. Configure the Ordering API connection string for the SQL Server reachable from the process. For an API running directly on the host, the Compose-published database host is `localhost` on port `1433`. For an API container on the same Compose network, use the database service name `order-db` and container port `1433`, not `localhost`.
3. From `E-commerce/src`, run:

   ```bash
   dotnet run --project Services/Ordering/Ordering.API
   ```

4. In Development, startup attempts to apply migrations and seed data. Check the API console output if startup fails; a published Docker port alone does not mean SQL Server has finished starting or accepted connections.
5. Call the HTTP endpoints at `http://localhost:5003` (or use the HTTPS launch profile at `https://localhost:5053`).

**Compose coverage limitation:** `compose.yaml` currently defines the `order-db` container, but it does not define an `ordering-api` container. Therefore `docker compose up` starts the Ordering database but does not start the Ordering API. Run the API with `dotnet run` unless and until an API service is added to Compose.

Do not put real passwords or payment information in source-controlled settings, examples, logs, or screenshots. Use a local secret store or environment variables for development configuration. The current Compose file contains development credentials; treat them as local-only and replace them before using any shared or production environment.

## 9. How to add a new Ordering feature

Use this sequence for a new behavior such as canceling an order:

1. **Clarify the business rule.** Decide which statuses may transition to `Canceled`, whether cancellation is allowed after completion, and whether items or payment data change. Write down invalid transitions as well as the happy path.
2. **Put the invariant in the domain.** Add a method such as `Order.Cancel()` rather than setting `Status` directly from an endpoint. Have it reject disallowed transitions and raise an event if cancellation is a meaningful domain event.
3. **Create an application command and result.** Define a command record implementing the existing `ICommand<T>` pattern. Add a FluentValidation validator for request-level checks.
4. **Implement a handler.** Load the aggregate through `IApplicationDbContext`, handle the not-found case using the existing application exception pattern, call the domain method, and save using the provided cancellation token.
5. **Add an API endpoint.** Add a Carter module that binds HTTP input, sends the command through `ISender`, awaits it, maps the result, and returns the appropriate status code.
6. **Add tests.** Test domain transitions independently, validator behavior, handler outcomes (success/not found), and the HTTP binding/response contract if API integration tests exist.
7. **Change persistence only if necessary.** If the data model changes, update EF configuration and add a migration. Do not edit an already-applied migration to represent a new schema change.
8. **Review observability and compatibility.** Decide whether a domain event is needed, avoid exposing internal exception details, and consider whether the new response is backward compatible for existing clients.

For read behavior, add a query and query handler rather than reusing a command handler. Keep queries read-only, use `AsNoTracking()` where appropriate, include or project only the data needed for the response, and apply stable ordering before pagination.

## 10. Current limitations and production considerations

These points describe gaps or risks visible in the current code. They are not claims that the service already handles them.

### 10.1 Payment data

`PaymentDto` and the domain `Payment` contain card number and CVV fields. The DTO mapping returns those values as part of order responses, and EF maps payment fields into the order record. This is not an appropriate production design for raw payment-card data. In particular, CVV must not be stored after authorization. Use a payment provider/token and persist only the minimum provider reference and safe display metadata required by the business. Never log these fields.

### 10.2 Validation and trust boundaries

The current validators are basic. Create validation checks order name, customer ID, and item presence; domain value factories then check some field constraints. Before production, validate every required nested field, enforce maximum lengths, validate the `OrderStatus` transitions, and verify that referenced customers/products exist and are allowed for the caller.

The create handler accepts item prices from the request and uses those prices directly. A client-controlled price is not trustworthy. A robust implementation should load authoritative product prices from the catalog or a trusted pricing source and calculate the order total on the server. Also consider how the Ordering service obtains and verifies data owned by other services; do not create cross-service database foreign keys in a microservice architecture.

### 10.3 Aggregate behavior and update semantics

`UpdateOrderCommandHandler` changes the order's name, addresses, payment, and status, but does not replace or update order items. That may be intentional; if so, document it in the API contract. If item changes are allowed, define explicit add/remove/change-quantity operations and enforce the rules in `Order`.

The current update path accepts a status from the DTO and passes it to `Order.Update`; it does not enforce a transition graph. A status enum restricts the set of representable labels, but not the legal transitions between them.

### 10.4 Domain-event transaction reliability

`DispatchDomainEventInterceptor` publishes events during EF's `SavingChanges` callback and clears events before the database save has completed. This means a handler can observe an event before the transaction has committed; if the later database save fails, the external side effect may describe data that was never persisted. Clearing first can also lose pending events if dispatch or persistence fails.

The current handlers only log, so the risk is limited today. If events later cause external side effects, use a reliable design such as a transactional outbox: write the order and an outbox record in the same database transaction, then publish the outbox record asynchronously with retries and idempotency.

### 10.5 Audit identity and timestamps

`AuditableEntityInterceptor` currently writes the hard-coded user name `"suman"`. This is useful only as a development placeholder. Production audit values should come from a trusted user/service identity abstraction. Ensure timestamps have a consistent UTC policy.

### 10.6 Consistency and error handling improvements

- `Order.Create` initializes status to `Pending`, while the EF mapping declares a database default of `Draft`. Align these defaults so behavior is clear even for inserts that bypass normal construction paths.
- Several ID factories use the message `"CustomerId is empty"` even for product or order-item IDs. Correct messages improve diagnostics.
- `Guid` is a value type, so a null check in an ID factory does not replace the meaningful `Guid.Empty` check.
- `GetOrderQueryHandler` counts all orders before querying one page. That is reasonable for a total-count response, but the `PaginationRequest.count` property is not used; clarify or remove it. Validate page index and page size and impose a maximum page size.
- Stable ordering should use a unique tie-breaker (for example, order name followed by ID) so rows do not move unpredictably between pages when sort values are equal.
- Consider mapping not-found and validation exceptions to consistent HTTP Problem Details responses. The API currently declares some problem response metadata, but endpoint declarations alone do not implement exception handling.
- `InitialiseDatabaseAsync` should await the migration operation instead of blocking on it.
- Delete currently removes the order without a domain event. Decide whether deletion needs an audit record or a business event.

## 11. Verification and troubleshooting

When diagnosing a failure, follow the request path:

1. **Connection/startup:** Confirm SQL Server is running and reachable on the correct host/port; inspect the API startup error before testing an endpoint.
2. **Schema:** Confirm the expected migration is applied. In Development, startup invokes migration automatically; outside Development, the code does not call the startup initializer.
3. **Request binding:** Check the route, JSON property structure, and query parameter names. Pagination is bound from query parameters using `[AsParameters]`.
4. **Validation/domain construction:** Check the FluentValidation errors first, then domain factory or aggregate exceptions for deeper invariants.
5. **Handler/persistence:** Confirm the handler is discovered by MediatR, includes the expected related data for reads, and awaits database operations.
6. **Mapping/response:** Verify Mapster receives the completed result (not a `Task`) and that domain-to-DTO conversion includes only fields safe to return.

Useful local commands:

```bash
# From E-commerce/src
docker compose ps
docker compose logs --tail=100 order-db
dotnet build Services/Ordering/Ordering.API/Ordering.API.csproj
dotnet run --project Services/Ordering/Ordering.API
```

For schema changes, create a new migration using the Infrastructure project as the migrations project and the API project as the startup project, then review the generated migration before applying it. Use the EF tooling version compatible with the EF Core packages in the solution.

## 12. A mental model to keep

When an order is created, the API should only translate HTTP into a command; the application handler should coordinate the use case; the domain aggregate should enforce business rules; and Infrastructure should translate the resulting state into database operations. On reads, query handlers retrieve and shape data for DTOs without changing the aggregate.

That division is the practical purpose of the architecture: business rules stay close to the business model, infrastructure details stay replaceable, and the API remains a transport boundary. The design works best when each layer has a clear responsibility, invariants are enforced in the domain, database and external-service data are treated as untrusted inputs, and asynchronous work is awaited all the way through.
