# Ordering Domain Abstractions

This note documents the relationships between the classes and interfaces in
this folder and how they support Domain-Driven Design (DDD).

## Relationship diagram

```text
                         +--------------------------+
                         |         IEntity          |
                         |--------------------------|
                         | CreatedAt                |
                         | CreatedBy                |
                         | LastModified             |
                         | LastModifiedBy           |
                         +-------------+------------+
                                       |
                                       | inherited by
                                       v
                         +--------------------------+
                         |        IEntity<T>        |
                         |--------------------------|
                         | T Id                     |
                         +-------------+------------+
                                       |
                                       | implemented by
                                       v
                         +--------------------------+
                         |         Entity<T>        |
                         |--------------------------|
                         | Identity and audit data  |
                         +-------------+------------+
                                       |
                                       | inherited by
                                       v
                         +--------------------------+
                         |       Aggregate<TId>     |
                         |--------------------------|
                         | Domain event collection  |
                         | AddDomainEvent()        |
                         | ClearDomainEvents()     |
                         +-------------+------------+
                                       |
                                       | implements
                                       v
                         +--------------------------+
                         |      IAggregate<TId>     |
                         +-------------+------------+
                                       |
                                       | inherits
                                       v
                         +--------------------------+
                         |        IAggregate        |
                         |--------------------------|
                         | IEntity                  |
                         | DomainEvent              |
                         | ClearDomainEvents()      |
                         +-------------+------------+
                                       |
                                       | stores
                                       v
                         +--------------------------+
                         |       IDomainEvent       |
                         |--------------------------|
                         | EventId                  |
                         | OccuredOn                |
                         | EventType                |
                         | MediatR.INotification    |
                         +--------------------------+
```

## Class and interface responsibilities

### `IEntity`

`IEntity` defines the audit contract shared by domain objects:

- Creation date and creator
- Last-modification date and modifier

Audit information is cross-cutting domain metadata. It should not contain
business rules.

### `IEntity<T>`

`IEntity<T>` extends `IEntity` with a strongly typed identifier:

```csharp
public interface IEntity<T> : IEntity
{
    T Id { get; set; }
}
```

An entity is identified by its identity, not only by its property values. The
generic identifier allows different entities to use `Guid`, `int`, or a
domain-specific identifier type.

### `Entity<T>`

`Entity<T>` is the reusable implementation of `IEntity<T>`. It centralizes
identity and audit properties so concrete entities do not repeat them.

It is abstract because the domain should create meaningful concrete entities,
such as `Order`, `Customer`, or `Product`, rather than a generic entity.

### `IAggregate`

`IAggregate` extends the entity contract with domain-event access:

- Read the events raised by the aggregate
- Clear events after they have been collected for dispatch

In DDD, an aggregate is a consistency boundary. Business rules involving the
aggregate and its child objects should be enforced through the aggregate root.

### `IAggregate<T>`

`IAggregate<T>` combines the aggregate contract with a typed entity identity.
It represents an aggregate root that has:

- A typed `Id`
- Audit information
- Domain events
- Event-clearing behavior

### `Aggregate<TId>`

`Aggregate<TId>` is the base implementation for aggregate roots. It inherits
identity and audit properties from `Entity<TId>` and maintains a private list
of domain events.

The event list is exposed as `IReadOnlyList<IDomainEvent>` so external code can
inspect events but cannot directly add or remove them. The aggregate remains
responsible for recording events when business operations change its state.

Example:

```csharp
public sealed class Order : Aggregate<Guid>
{
    public void Confirm()
    {
        // Validate business rules and change state here.
        AddDomainEvent(new OrderConfirmedEvent(Id));
    }
}
```

### `IDomainEvent`

`IDomainEvent` represents a meaningful business occurrence, such as:

- `OrderPlaced`
- `OrderConfirmed`
- `PaymentReceived`
- `OrderCancelled`

It inherits from MediatR's `INotification`, which allows the application
layer to dispatch the event to notification handlers without coupling the
domain model to a particular handler.

The intended flow is:

```text
Aggregate changes state
        |
        v
Aggregate records a domain event
        |
        v
Unit of Work persists the aggregate
        |
        v
Application/infrastructure dispatches the event
        |
        v
Handlers react to the event
```

## Why these relationships are defined

```text
IEntity<T>  -> defines identity
Entity<T>   -> implements reusable entity state
IAggregate  -> defines aggregate/event behavior
Aggregate<T>-> implements the aggregate boundary
IDomainEvent-> describes business occurrences
```

Together, these abstractions separate responsibilities:

1. **Entities own identity.** Two objects with the same values can still be
   different entities when their identifiers differ.
2. **Aggregates protect invariants.** State changes that must remain
   consistent should be performed through the aggregate root.
3. **Domain events communicate business facts.** Other parts of the system can
   react without placing messaging, email, or integration code inside the
   aggregate.
4. **Interfaces define contracts.** Application and infrastructure code can
   depend on domain capabilities instead of concrete implementation details.
5. **Generic identifiers preserve type safety.** An order identifier and a
   customer identifier can use different types and cannot be confused easily.

## DDD implementation tips

- Put business behavior in concrete aggregates and entities, not only in
  application services.
- Keep aggregate boundaries small. Include an object inside an aggregate only
  when it must participate in the same consistency transaction.
- Do not expose mutable collections from an aggregate. Use methods such as
  `AddItem`, `RemoveItem`, `Confirm`, and `Cancel` to enforce invariants.
- Raise domain events only after the aggregate has validated the business
  operation and changed its state.
- Dispatch domain events outside the aggregate, normally after persistence
  succeeds.
- Domain events should describe something that happened, rather than request
  something to happen. Prefer `OrderConfirmed` over `ConfirmOrder`.
- Keep domain abstractions independent of database and transport concerns.
- Use UTC timestamps in distributed systems.
- Consider renaming `DomainEvent` to `DomainEvents` because it represents a
  collection.
- Consider correcting `OccuredOn` to `OccurredOn`.
- Event metadata should normally be assigned once when the event is created.
  A property that generates a new `Guid` every time it is read can produce
  inconsistent event identity.
- Consider making `AddDomainEvent` part of an internal/protected design rather
  than exposing event mutation broadly. The aggregate should control when an
  event is raised.

## Example aggregate structure

```text
Order : Aggregate<Guid>
|
|-- Id, CreatedAt, LastModified
|-- OrderItems
|-- Status
|-- Confirm()
|-- Cancel()
|-- AddItem()
|
`-- raises OrderConfirmedEvent : IDomainEvent
                              |
                              `-- handled by application/infrastructure
```

The domain project currently provides the foundation. Concrete aggregates,
value objects, domain services, repositories, and event handlers should be
added around the ordering business rules as the model grows.
