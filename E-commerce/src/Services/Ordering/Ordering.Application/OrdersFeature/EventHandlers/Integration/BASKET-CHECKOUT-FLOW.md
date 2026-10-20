# Basket checkout to order creation

This memo explains how a checkout request in Basket reaches order persistence in
Ordering. The path crosses two different dispatch mechanisms:

- **MassTransit + RabbitMQ** carries an integration event between services.
- **MediatR** dispatches commands inside each service.

They are separate connections: RabbitMQ connects Basket to Ordering; MediatR
connects a command to its handler within a service.

## End-to-end diagram

```text
CLIENT
  |
  | POST /basket/checkout
  | CheckoutBasketRequest (checkout details)
  v
BASKET SERVICE — HTTP endpoint
  CheckoutBasketEndpoint
  | adapts request to CheckoutBasketCommand
  | sender.Send(command)                         [MediatR]
  v
  CheckoutBasketHandler.Handle()
  | reads the saved basket using UserName
  | adapts checkout DTO to BasketCheckoutEvent
  | replaces event.TotalPrice with basket.TotalPrice
  | publishEndpoint.Publish(event)                [MassTransit]
  |                                                |
  |                       RabbitMQ message broker |
  |                                                v
  |                                  ORDERING SERVICE — consumer
  |                                  BasketCheckoutEventHandler
  |                                  Consume(event)
  |                                  | maps event data to OrderDto
  |                                  | creates CreateOrderCommand
  |                                  | sender.Send(command) [MediatR]
  |                                  v
  |                                  CreateOrderCommandHandler.Handle()
  |                                  | builds Order domain entity
  |                                  | adds order and saves changes
  |                                  v
  |                                  ORDERING DATABASE
  |
  | after publish succeeds, deletes the saved basket
  v
Basket API returns its checkout response
```

The diagram shows the logical sequence. The order work happens asynchronously
after the event is published; the Basket API does not wait for the Ordering
consumer to finish saving the order.

## Step by step

### 1. The client calls the Basket endpoint

`CheckoutBasketEndpoint` handles `POST /basket/checkout`. It adapts the request
to a `CheckoutBasketCommand` and calls `ISender.Send`. This is an in-process
MediatR dispatch in Basket.

File: `Services/Basket/BasketAPI/BasketAPI/Features/CheckoutBasket/CheckoutBasketEndpoint.cs`

### 2. MediatR invokes the Basket command handler

MediatR finds `CheckoutBasketHandler`, which handles
`CheckoutBasketCommand`. The handler loads the basket using the submitted user
name. If there is no basket, it returns `false` and does not publish an event.

When the basket exists, it adapts the submitted `BasketCheckoutDto` into a
`BasketCheckoutEvent`. Matching fields such as the customer, address, and
payment details are copied by name. It then sets `TotalPrice` from the
server-side basket before publishing.

File: `Services/Basket/BasketAPI/BasketAPI/Features/CheckoutBasket/CheckoutBasketHandler.cs`

### 3. Basket publishes an integration event to RabbitMQ

The handler calls `IPublishEndpoint.Publish(event)`. This publishes a message
of type `BasketCheckoutEvent` through MassTransit to RabbitMQ. It is not a
direct method call to `BasketCheckoutEventHandler`.

Both services configure MassTransit with the shared
`AddMessageBroker` extension. That extension uses the RabbitMQ host and
credentials from `MessageBroker` configuration. Basket registers the broker in
its `Program.cs`.

Shared contract:
`BuildingBlocks/BuildingBlocks.Messaging/Events/BasketCheckoutEvent.cs`

Broker setup:
`BuildingBlocks/BuildingBlocks.Messaging/MassTransit/Extension.cs`

### 4. Ordering is registered to consume that event type

Ordering's `DependencyInjection.AddApplicationServices` calls
`AddMessageBroker` and passes the Ordering.Application assembly. The extension
scans that assembly for MassTransit consumers. It finds
`BasketCheckoutEventHandler` because it implements
`IConsumer<BasketCheckoutEvent>`.

MassTransit configures consumer endpoints with `ConfigureEndpoints`. When the
message for `BasketCheckoutEvent` arrives on the configured RabbitMQ endpoint,
MassTransit calls the consumer's `Consume` method.

Files:

- `Services/Ordering/Ordering.Application/DependencyInjection.cs`
- `Services/Ordering/Ordering.Application/OrdersFeature/EventHandlers/Integration/BasketCheckoutEventHandler.cs`

### 5. The consumer dispatches a separate command through MediatR

`BasketCheckoutEventHandler.Consume` receives the event, maps its data into an
`OrderDto`, wraps that DTO in a `CreateOrderCommand`, then calls
`sender.Send(command)`.

This second `Send` is not a RabbitMQ operation. It is an in-process MediatR
dispatch in Ordering. MediatR invokes `CreateOrderCommandHandler` because that
class implements `ICommandHandler<CreateOrderCommand, CreateOrderResult>`, and
Ordering registers its application assembly with MediatR.

Consumer:
`Services/Ordering/Ordering.Application/OrdersFeature/EventHandlers/Integration/BasketCheckoutEventHandler.cs`

Command and validation:
`Services/Ordering/Ordering.Application/OrdersFeature/Commands/CreateOrder/CreateOrderCommand.cs`

### 6. The command handler creates and saves the order

`CreateOrderCommandHandler.Handle` turns the DTO into an Order domain entity,
adds it to `dbContext.Orders`, and calls `SaveChangesAsync`. That is the point
where this flow persists the order to the Ordering database.

File:
`Services/Ordering/Ordering.Application/OrdersFeature/Commands/CreateOrder/CreateOrderCommandHandler.cs`

## What each message means

| Message | Where it goes | Purpose |
| --- | --- | --- |
| `CheckoutBasketCommand` | Inside Basket through MediatR | Ask Basket to check out the user's basket |
| `BasketCheckoutEvent` | Basket to Ordering through RabbitMQ/MassTransit | Announce that checkout was requested with checkout details |
| `CreateOrderCommand` | Inside Ordering through MediatR | Ask Ordering to create and persist an order |

An **endpoint** receives the HTTP request; a **command** asks one service to do
work; an **event** carries information between services; a **consumer** receives
an event; a **handler** performs the command's work.

## Important details in the current implementation

- **The published total is server-derived.** The event is initially adapted
  from the client DTO, but Basket overwrites `TotalPrice` with the stored
  basket's total so the client-supplied price is not authoritative.
- **Ordering currently does not use that total.** The consumer's mapping does
  not read `message.TotalPrice`; it currently constructs two hard-coded order
  items and prices. Consequently, the persisted order items are not derived
  from the Basket contents in this code yet.
- **Billing and shipping currently use the same address.** The consumer creates
  one `AddressDto` and assigns it to both `BillingAddress` and
  `ShippingAddress`.
- **Publishing is not confirmation of order creation.** Basket publishes the
  event and then deletes the basket; the consumer processes the event
  independently. A successful Basket response therefore does not by itself
  mean the order has already been saved.
- **Payment details are included in the event contract.** The current event
  includes card fields such as card number and CVV. Treat these as sensitive
  data; production payment flows should avoid passing raw card data through
  general application messaging and should use a compliant payment provider or
  tokenized payment reference.

## Where to look when tracing the flow

Start at the HTTP route, follow the Basket `sender.Send`, then the
`IPublishEndpoint.Publish`. For the receiving side, search for
`IConsumer<BasketCheckoutEvent>` and follow its `sender.Send` to the
`ICommandHandler<CreateOrderCommand, CreateOrderResult>` implementation.
