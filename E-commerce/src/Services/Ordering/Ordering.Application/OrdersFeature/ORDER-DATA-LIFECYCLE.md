# Order data lifecycle: HTTP, application, domain, database, response

This memo follows order data in both directions through the Ordering service:

1. Creating an order: HTTP request body → command → domain objects → database.
2. Reading orders: database → domain objects → DTOs → HTTP response body.

The key idea is that the request body is **not** stored as-is. Each layer has
its own responsibility and representation. The API/application DTO is a
transport shape; the domain model expresses business concepts and rules; EF
Core maps the domain model to the database schema.

## Create order: end-to-end diagram

```text
CLIENT JSON
  |
  | POST /orders
  | CreateOrderRequest { OrderDto }
  v
ORDERING.API — CreateOrder endpoint
  | request.Adapt<CreateOrderCommand>()
  | ISender.Send(command)                      [MediatR, in process]
  v
ORDERING.APPLICATION — CreateOrderCommand
  | validation behavior runs before handler
  v
CreateOrderCommandHandler.Handle()
  | OrderDto → Address / Payment value objects
  | Order.Create(...) → Order aggregate
  | Order.Add(...) → OrderItem entities
  | dbContext.Orders.Add(order)
  | dbContext.SaveChangesAsync()                [IApplicationDbContext]
  v
ORDERING.INFRASTRUCTURE — ApplicationDbContext / EF Core
  | maps value objects, IDs, relationships, and fields
  | executes database INSERTs
  v
ORDERING DATABASE

Then on the way back:
  CreateOrderResult { Id }
    → CreateOrderResponse { Id }
    → HTTP 201 Created, Location: /orders/{id}
```

## Create order: what happens at each step

### 1. JSON is bound to the API request type

The `POST /orders` endpoint accepts `CreateOrderRequest`, whose `Order`
property is an `OrderDto`. The JSON therefore has an outer `order` object
(property-name casing follows ASP.NET Core's JSON settings):

```json
{
  "order": {
    "id": "client-supplied-guid",
    "customerId": "customer-guid",
    "orderName": "example-order",
    "shippingAddress": { "firstName": "...", "lastName": "...", "emailAddress": "...", "addressLine": "...", "country": "...", "state": "...", "zipCode": "..." },
    "billingAddress": { "firstName": "...", "lastName": "...", "emailAddress": "...", "addressLine": "...", "country": "...", "state": "...", "zipCode": "..." },
    "payment": { "cardName": "...", "cardNumber": "...", "expiration": "...", "cvv": "...", "paymentMethod": 1 },
    "status": 0,
    "orderItems": [
      { "orderId": "client-supplied-guid", "productId": "product-guid", "quantity": 1, "price": 10.0 }
    ]
  }
}
```

This is an outline of the DTO's fields, not a recommendation to send raw card
data. See the security note below.

`CreateOrder` adapts the request to `CreateOrderCommand` and sends it via
MediatR. This adaptation wraps/copies the DTO into the command; it does not
create the domain aggregate or save anything.

Files:

- `Services/Ordering/Ordering.API/Endpoints/CreateOrder.cs`
- `Services/Ordering/Ordering.Application/Dtos/OrderDto.cs`
- `Services/Ordering/Ordering.Application/OrdersFeature/Commands/CreateOrder/CreateOrderCommand.cs`

### 2. Application validation runs

The registered MediatR validation behavior runs the command's
`CreateOrderCommandValidator` before the command handler. The current validator
checks that `OrderName` is not empty, `CustomerId` is not empty, and there is at
least one order item. This is application-level validation; it is not the same
as the domain model's own invariants.

### 3. The application handler constructs domain objects

`CreateOrderCommandHandler` receives the `OrderDto` and explicitly maps it:

- Address DTO values become `Address` value objects via `Address.Of`.
- Payment DTO values become a `Payment` value object via `Payment.Of`.
- `Order.Create(...)` creates the `Order` aggregate.
- For each item DTO, `Order.Add(...)` creates an `OrderItem` entity.

The domain methods own business checks. For example, `Order.Add` rejects
quantity or price less than or equal to zero; `Address.Of` requires email and
address line; `Payment.Of` requires card name/number and checks the CVV length.
The handler is the mapper/orchestrator; it is not simply saving the incoming
JSON object.

File:
`Services/Ordering/Ordering.Application/OrdersFeature/Commands/CreateOrder/CreateOrderCommandHandler.cs`

### 4. The domain model supplies its own state and identity

The handler currently passes a newly generated `Guid` to `Order.Create`, so
`Order.Id` comes from the server, not the `OrderDto.Id` in the request. The
domain creates the order with `Pending` status. Each `OrderItem` also gets its
own generated entity ID and uses the new order's ID as its foreign key.

Therefore, in the current create path:

| Request field | Current behavior |
| --- | --- |
| `OrderDto.Id` | Ignored; handler generates the order ID |
| `OrderDto.Status` | Ignored; `Order.Create` sets `Pending` |
| `OrderItemDto.OrderId` | Ignored; `Order.Add` uses the new order ID |
| `CustomerId`, name, addresses, payment, item product/quantity/price | Mapped into domain state (subject to validation and domain rules) |
| `TotalPrice` | Not present in `OrderDto`; the domain calculates it from item prices and quantities |

These are facts about this implementation, not rules imposed by Clean
Architecture in general.

Domain files:

- `Services/Ordering/Ordering.Domain/Model/Order.cs`
- `Services/Ordering/Ordering.Domain/Model/OrderItem.cs`
- `Services/Ordering/Ordering.Domain/ValueObjects/Address.cs`
- `Services/Ordering/Ordering.Domain/ValueObjects/Payment.cs`

### 5. EF Core persists the domain model

The handler adds the `Order` aggregate to `IApplicationDbContext.Orders` and
calls `SaveChangesAsync`. The application depends on the context abstraction;
the actual `ApplicationDbContext` is in Infrastructure and EF Core performs
the database mapping and writes.

`ApplicationDbContext` applies its entity configurations from the Infrastructure
assembly. Those configurations define, among other things, key conversions for
value-object IDs, the order/customer and order-item relationships, complex
properties for addresses and payment, and how status is stored.

Files:

- `Services/Ordering/Ordering.Infrastructure/Data/ApplicationDbContext.cs`
- `Services/Ordering/Ordering.Infrastructure/Data/Configurations/OrderConfiguration.cs`
- `Services/Ordering/Ordering.Infrastructure/Data/Configurations/OrderItemConfiguration.cs`

### 6. The create response is not the full saved order

After saving, the handler returns `CreateOrderResult` containing the new order
ID. The endpoint adapts that result to `CreateOrderResponse` and returns HTTP
`201 Created` with a location such as `/orders/{id}`. The response body is
currently only:

```json
{
  "id": "server-generated-order-guid"
}
```

It does not return the entire `OrderDto`.

## Read order: end-to-end diagram

```text
CLIENT
  |
  | GET /orders?pageIndex=1&pageSize=10
  | or GET /orders/{orderName}
  | or GET /orders/customer/{customerId}
  v
ORDERING.API — GET endpoint
  | creates query from route/query parameters
  | ISender.Send(query)                         [MediatR, in process]
  v
ORDERING.APPLICATION — query handler
  | dbContext.Orders ...                         [IApplicationDbContext]
  | EF Core reads rows and materializes entities/value objects
  | loads OrderItems where required
  | Order aggregate(s) → OrderDto(s)             [OrdersToOrderDtoList]
  v
Query result containing DTO(s)
  |
  | endpoint adapts result to response wrapper
  | Results.Ok(response)
  v
CLIENT JSON RESPONSE
```

### Reading step by step

1. A GET endpoint binds route or query-string values and creates a query object.
   The endpoint sends it through MediatR.
2. The matching query handler uses `IApplicationDbContext` to ask EF Core for
   orders. The list query includes order items, uses `AsNoTracking`, and applies
   ordering/pagination. The name and customer queries also load order items.
3. EF Core materializes database values as `Order`/`OrderItem` domain objects,
   including their configured value objects and ID conversions.
4. `OrdersToOrderDtoList` explicitly projects each domain object to
   `OrderDto`: value-object IDs/names become primitive GUIDs/strings; addresses
   and payment become DTOs; order items become `OrderItemDto` values.
5. The query handler returns the DTO result. The endpoint adapts it to its
   response wrapper and serializes that wrapper as JSON.

Read-path files:

- `Services/Ordering/Ordering.API/Endpoints/GetOder.cs` — paginated list at `GET /orders/`
- `Services/Ordering/Ordering.API/Endpoints/GetOderByName.cs` — `GET /orders/{orderName}`
- `Services/Ordering/Ordering.API/Endpoints/GetOrderByCustomer.cs` — `GET /orders/customer/{customerId}`
- `Services/Ordering/Ordering.Application/OrdersFeature/Queries/GetOrder/GetOrderQueryHandler.cs`
- `Services/Ordering/Ordering.Application/OrdersFeature/Queries/GetOrderByName/GetOrderByNameQueryHandler.cs`
- `Services/Ordering/Ordering.Application/OrdersFeature/Queries/GetOrderByCustomer/GetOrderByCustomerHandler.cs`
- `Services/Ordering/Ordering.Application/Extension/OrderExtension.cs`

The exact response wrapper depends on the endpoint. For example, the paginated
list returns a `GetOrderResponse` containing pagination metadata and
`OrderDto` entries; the name/customer endpoints return wrappers containing
collections of `OrderDto`.

## The important distinction

```text
HTTP request/response  = public JSON contract at the API boundary
DTO                    = data shape passed between API and application code
Command/query          = application request to perform work or retrieve data
Domain entity/value    = business state and rules
EF Core configuration = mapping between domain objects and database storage
```

The outbound read flow reverses the data direction, but it is not a raw
database-row-to-JSON conversion: the query handler projects domain objects into
DTOs, and the endpoint returns a response contract.

## Current implementation cautions

- The response projection includes payment card number and CVV in `OrderDto`.
  Returning or storing raw card details is unsafe for a production payment
  system. Use a payment provider and token/reference instead, and never expose
  CVV in API responses.
- `Order.TotalPrice` is calculated from order items in the domain, but it is
  not a property of `OrderDto`, so the current read projection does not include
  it in API responses.
- A database entity is not the request DTO: create logic deliberately
  generates identity/status and builds validated domain objects. When
  debugging, follow the explicit mapping in the handler and
  `OrdersToOrderDtoList` rather than assuming properties transfer automatically.
