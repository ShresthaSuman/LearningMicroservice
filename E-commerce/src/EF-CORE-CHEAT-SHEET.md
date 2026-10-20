# Entity Framework Core Cheat Sheet

A practical guide to EF Core concepts, database flow, migrations, CLI usage,
implementation patterns, and common failure cases. Examples use C#, SQL Server,
and the Ordering service in this repository. Replace provider-specific setup if
your application uses PostgreSQL, SQLite, or another database.

## 1. What EF Core does

Entity Framework Core (EF Core) is an object-relational mapper (ORM). It maps
between:

- **C# model:** `DbContext`, entity classes, value objects, and relationships.
- **Relational model:** tables, columns, keys, indexes, and foreign keys.
- **Database operations:** SQL statements generated for queries and writes.

EF Core does not remove the need to understand SQL. It generates SQL from LINQ,
tracks changes to entities, and coordinates persistence, but query shape,
indexes, transactions, and database permissions still matter.

## 2. End-to-end database flow

```text
Application startup
  |
  | Read configuration (connection string, provider)
  v
Dependency injection registers DbContext options
  |
  +------------------------------+
  |                              |
  v                              v
Application request          EF CLI design time
  |                              |
  | Inject DbContext             | Build startup project and create context
  v                              v
DbContext + model configuration  Model snapshot + migration operations
  |                              |
  | LINQ query / entity changes  | dotnet ef database update
  v                              v
EF translates LINQ to SQL     Apply migration SQL
  |                              |
  +---------------+--------------+
                  v
          Database provider
                  |
                  v
             Database
```

### Runtime read/write path

```text
HTTP endpoint
  -> application handler/service
  -> DbContext (unit of work)
  -> DbSet / LINQ query
  -> EF query translation and parameterization
  -> SQL Server provider
  -> SQL Server

For writes:
entity changes -> ChangeTracker -> SaveChangesAsync()
                -> INSERT / UPDATE / DELETE in a transaction
```

`AddDbContext<TContext>()` normally gives a scoped context in ASP.NET Core:
one context per request scope. A `DbContext` is not thread-safe; do not run
parallel operations on the same instance.

## 3. The main EF Core building blocks

### `DbContext`

The session/unit of work coordinating a database connection, model metadata,
queries, change tracking, and saving:

```csharp
public sealed class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(ApplicationDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
```

Prefer injecting a context into an application service or handler. Do not
create a new context for every repository method within one use case if that
breaks the transaction/unit-of-work boundary.

### `DbSet<TEntity>`

A typed entry point for querying and adding entities:

```csharp
var order = await db.Orders
    .SingleOrDefaultAsync(o => o.Id == orderId, cancellationToken);
```

### Entity classes

An entity usually has a key and properties. EF can use private setters and
constructors for many domain models, but constructor binding only uses mapped
properties and follows EF's parameter/property matching rules. If a value
object is stored in one scalar column, configure a value converter rather than
mapping it as a separate entity.

### Model configuration

Use `IEntityTypeConfiguration<TEntity>` for focused table mapping and
relationships:

```csharp
public sealed class CustomerConfiguration
    : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Name)
            .HasMaxLength(100)
            .IsRequired();
    }
}
```

Apply configurations from the context's assembly as shown above. Fluent
configuration is generally clearer than relying on conventions for important
constraints. Data annotations are useful for simple rules but should not be
duplicated unnecessarily.

## 4. Configure the provider and connection

The Ordering infrastructure project registers SQL Server:

```csharp
services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
```

Its connection string is retrieved using the key `ConnectionStrings:Database`.
An example local development connection string is:

```json
{
  "ConnectionStrings": {
    "Database": "Server=localhost,1433;Database=OrderDb;User Id=sa;Password=YOUR_LOCAL_SECRET;Encrypt=False;TrustServerCertificate=True"
  }
}
```

Do not commit real passwords. Use .NET user secrets locally, environment
variables in containers, and a managed secret store in production.

```bash
# Run from the Ordering.API project directory.
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:Database" \
  "Server=localhost,1433;Database=OrderDb;User Id=sa;Password=YOUR_PASSWORD;Encrypt=False;TrustServerCertificate=True"
```

When connecting from the host machine to the repository's SQL Server Compose
service, use `localhost,1433`. From another container on the Compose network,
use the SQL Server service/container DNS name and port `1433`, not `localhost`.
`localhost` inside a container means that same container.

The Compose file currently initializes the SQL Server `sa` account using
`MSSQL_SA_PASSWORD`. If an application should connect as `admin`, create that
SQL login explicitly and grant only the permissions it needs. Changing an
environment variable does not change credentials in an already-initialized
database volume.

## 5. EF Core CLI setup and project selection

The EF CLI needs to know two projects:

- **Target project (`--project`):** where migration files are stored; normally
  the project containing the `DbContext`.
- **Startup project (`--startup-project`):** the executable project EF builds
  to load configuration and create the context.

For this repository:

```text
Ordering.API            startup/configuration
    |
    +--> Ordering.Infrastructure   DbContext, provider, migrations
    |
    +--> Ordering.Application
```

Install or update the `dotnet-ef` tool and check its version:

```bash
dotnet tool install --global dotnet-ef --version 10.0.12
# If already installed:
dotnet tool update --global dotnet-ef --version 10.0.12
dotnet ef --version
```

Keep `dotnet-ef`, `Microsoft.EntityFrameworkCore.Design`,
`Microsoft.EntityFrameworkCore.Tools`, and the EF runtime/provider packages on
compatible major versions. The Ordering infrastructure project currently
targets .NET 10 and references EF Core SQL Server 10.0.12; its Tools package
should be checked for version alignment if design-time commands fail.

The examples below assume the current directory is
`Services/Ordering/Ordering.Infrastructure`:

```bash
dotnet ef dbcontext info \
  --project Ordering.Infrastructure.csproj \
  --startup-project ../Ordering.API/Ordering.API.csproj
```

Explicitly specify both projects to avoid accidentally using another
`DbContext` or startup configuration in a multi-project solution.

## 6. Migrations: create, inspect, apply

A migration is a versioned description of model changes. It is not a live
database backup and does not automatically update production when code is
deployed.

### Create a migration

```bash
dotnet ef migrations add InitCreate \
  --output-dir Data/Migrations \
  --project Ordering.Infrastructure.csproj \
  --startup-project ../Ordering.API/Ordering.API.csproj \
  --context ApplicationDbContext
```

This normally creates:

- A migration file with `Up()` and `Down()` operations.
- A designer/metadata file.
- A model snapshot representing the current EF model.

Review migration files before applying them. Check for unintended table drops,
column type changes, data loss, bad defaults, and missing indexes.

### See migrations and generated SQL

```bash
dotnet ef migrations list \
  --project Ordering.Infrastructure.csproj \
  --startup-project ../Ordering.API/Ordering.API.csproj \
  --context ApplicationDbContext

dotnet ef migrations script \
  --project Ordering.Infrastructure.csproj \
  --startup-project ../Ordering.API/Ordering.API.csproj \
  --context ApplicationDbContext
```

Generate an idempotent deployment script when suitable for a database that may
be at different migration levels:

```bash
dotnet ef migrations script --idempotent \
  --project Ordering.Infrastructure.csproj \
  --startup-project ../Ordering.API/Ordering.API.csproj \
  --context ApplicationDbContext \
  --output orderdb-migrations.sql
```

### Apply migrations

```bash
dotnet ef database update \
  --project Ordering.Infrastructure.csproj \
  --startup-project ../Ordering.API/Ordering.API.csproj \
  --context ApplicationDbContext
```

Apply a specific migration by name:

```bash
dotnet ef database update InitCreate \
  --project Ordering.Infrastructure.csproj \
  --startup-project ../Ordering.API/Ordering.API.csproj \
  --context ApplicationDbContext
```

Roll back to an earlier migration (this can drop schema/data introduced later):

```bash
dotnet ef database update PreviousMigration \
  --project Ordering.Infrastructure.csproj \
  --startup-project ../Ordering.API/Ordering.API.csproj \
  --context ApplicationDbContext
```

Do not use automatic migration rollback as a substitute for a data recovery
plan.

### Remove or revert migration changes safely

If a migration has **not** been applied to a shared/production database:

```bash
dotnet ef migrations remove \
  --project Ordering.Infrastructure.csproj \
  --startup-project ../Ordering.API/Ordering.API.csproj \
  --context ApplicationDbContext
```

If it has already been applied to a database, first plan and execute a
controlled rollback or a new forward migration. Do not casually delete
migration files that databases have already recorded.

### Useful commands

```bash
dotnet ef dbcontext list
dotnet ef dbcontext info --context ApplicationDbContext
dotnet ef migrations has-pending-model-changes
dotnet ef database update 0
```

The last command targets migration `0` (the empty starting state), effectively
reverting all migrations. Use it only when that is intentional and safe.

## 7. Design-time `DbContext` creation

The EF CLI must create the context without handling an HTTP request. Commonly,
it builds the startup project, uses dependency injection, loads configuration,
and resolves the context.

Typical requirements:

1. The startup project builds.
2. The startup project references the infrastructure project.
3. The context has a constructor accepting
   `DbContextOptions<ApplicationDbContext>`.
4. The correct provider is configured.
5. The design package/tool is available and compatible.
6. The required configuration is available in the command's environment.

If host construction is unsuitable, implement
`IDesignTimeDbContextFactory<ApplicationDbContext>` in the target project. The
factory should use the same provider and a safely supplied design-time
connection string. Avoid embedding real credentials in the factory.

## 8. How model changes become a migration

```text
C# entity + Fluent configuration
                |
                v
      EF model metadata
                |
                | compare to model snapshot
                v
   Migration operations (Up/Down)
                |
                | provider translates operations
                v
       SQL script / commands
                |
                v
   __EFMigrationsHistory records applied migration
```

Adding a property does not change a database until a migration is generated and
applied. Conversely, editing entity code without adding a migration can make
the runtime model and database schema disagree.

## 9. Relationships and keys

Common relationship shapes:

```csharp
// One customer has many orders; each order has one customer.
builder.HasOne(o => o.Customer)
    .WithMany(c => c.Orders)
    .HasForeignKey(o => o.CustomerId)
    .IsRequired();
```

- **One-to-many:** FK normally lives on the many/dependent side.
- **One-to-one:** explicitly configure the dependent FK where ambiguous.
- **Many-to-many:** EF can create a join table, or map an explicit join entity
  when it has payload or domain behavior.

Use the related entity type in `HasOne<TEntity>()`, not the foreign key's CLR
type. For example, `HasOne<Product>()` is a relationship to `Product`;
`HasOne<ProductId>()` incorrectly asks EF to treat the ID value object as an
entity.

### Value-object IDs and value converters

If a strongly typed ID should be stored in a single database column, convert
between the value object and its primitive value:

```csharp
builder.Property(p => p.Id)
    .HasConversion(id => id.Value, value => ProductId.Of(value));
```

Apply the same conversion to a foreign-key property when it uses that ID type:

```csharp
builder.Property(item => item.ProductId)
    .HasConversion(id => id.Value, value => ProductId.Of(value));

builder.HasOne<Product>()
    .WithMany()
    .HasForeignKey(item => item.ProductId);
```

The principal key and dependent FK need compatible store types and matching
key configuration. Configure all strongly typed IDs consistently. EF cannot
bind arbitrary constructor parameters: parameter names/types must correspond to
mapped properties. The message “No suitable constructor was found” often means
EF mistakenly mapped a value object as an entity, or the selected constructor
does not match mapped properties.

If the value object has multiple persisted fields or its own identity/lifecycle,
consider EF owned entity types or a regular entity instead of a scalar
converter. An owned type is not a navigation target for constructor binding in
the same way as a scalar property.

## 10. Querying, tracking, and loading

### LINQ queries

LINQ expressions are translated to SQL only when they remain in the
translatable portion of the query. Materialization operators execute the
query:

```csharp
var orders = await db.Orders
    .Where(o => o.Status == OrderStatus.Pending)
    .OrderByDescending(o => o.CreatedAt)
    .Take(50)
    .ToListAsync(cancellationToken);
```

### Tracking choices

- **Tracking (default):** entities are tracked; edits can be persisted by
  `SaveChangesAsync()`.
- **No tracking:** appropriate for read-only results and can reduce tracking
  overhead.
- **No-tracking with identity resolution:** avoids duplicate object instances
  for the same key while not retaining normal context tracking.

```csharp
var products = await db.Products
    .AsNoTracking()
    .Where(p => p.Price > minimumPrice)
    .Select(p => new ProductSummary(p.Id, p.Name, p.Price))
    .ToListAsync(cancellationToken);
```

Project to DTOs for read endpoints instead of loading complete aggregate graphs
when only a few fields are needed.

### Related data loading

- **Eager:** `.Include(...)`; loads in the query, possibly with joins/splits.
- **Explicit:** load a navigation when needed through `Entry(...).LoadAsync()`.
- **Lazy:** loads on navigation access when configured; can hide database
  round-trips and trigger N+1 queries.

Use includes deliberately. Multiple collection includes can multiply rows;
consider `AsSplitQuery()` and inspect the generated SQL. Do not serialize
tracked EF entity graphs directly from APIs; cycles and unintended lazy loads
can result.

### Query performance checklist

- Filter, sort, and page in the database; avoid `.ToList()` before filtering.
- Use `AsNoTracking()` for read-only entity queries.
- Select only required columns.
- Add indexes for frequently filtered/joined/sorted columns, based on actual
  query plans and workload.
- Avoid N+1 patterns; inspect includes and query count.
- Use async EF APIs for I/O (`ToListAsync`, `SingleAsync`,
  `SaveChangesAsync`).
- Never execute concurrent operations on one context.
- Check generated SQL with logging or `ToQueryString()` during development.
- Parameterize raw SQL; do not concatenate untrusted input into SQL.

## 11. Change tracking and saving

For a tracked entity:

```csharp
var customer = await db.Customers
    .SingleAsync(c => c.Id == id, cancellationToken);

customer.ChangeName(newName);
await db.SaveChangesAsync(cancellationToken);
```

The context detects changes and creates SQL. For a new entity:

```csharp
db.Customers.Add(customer);
await db.SaveChangesAsync(cancellationToken);
```

For a disconnected entity from an API/request, avoid blindly calling
`Update(entity)` on a partially populated object: EF can mark every property as
modified and overwrite values. Load the existing entity and apply allowed
changes, or explicitly attach/set property states.

`SaveChanges` is transactional for the set of operations it performs when the
provider supports transactions. If a use case needs multiple saves or database
operations in one transaction, use an explicit transaction carefully. A
transaction does not include external services; use an outbox or another
reliable integration pattern for cross-service effects.

## 12. Concurrency and data integrity

### Optimistic concurrency

For SQL Server, a `rowversion` property can detect updates made by another
transaction:

```csharp
public byte[] Version { get; private set; } = [];

builder.Property(e => e.Version).IsRowVersion();
```

Catch `DbUpdateConcurrencyException`, reload or compare values, then apply a
defined conflict policy. Do not silently retry forever.

### Constraints

Enforce important integrity at both appropriate layers:

- Domain validation for business rules and useful errors.
- Database primary keys, foreign keys, unique constraints, nullability, and
  check constraints to protect persisted data under concurrent access.

Application-level “check then insert” is not a substitute for a unique index:
two concurrent requests may pass the check.

## 13. Common exceptional cases and what they mean

| Error or symptom | Common cause | What to check |
|---|---|---|
| `Unable to create a DbContext` | Startup project cannot create/configure it at design time | `--startup-project`, context constructor, provider registration, configuration, design-time factory |
| `No suitable constructor was found for type 'ProductId'` | EF interpreted an ID value object as an entity, or constructor parameters do not match mapped properties | Use `HasConversion` for scalar IDs; configure relationship to `Product`, not `ProductId` |
| `Login failed for user ...` | Wrong username/password, SQL login absent/disabled, or a persistent DB volume retained old credentials | Match connection string to configured SQL login; create `admin` explicitly if needed; inspect container logs |
| `Cannot open database ... requested by the login` | Login authenticates but lacks database access, or database does not exist | Grant user permissions; use `dbcreator` for creation if appropriate; verify database name |
| `A network-related or instance-specific error` | Wrong host/port, SQL Server not ready, firewall, or container networking error | Host uses `localhost,1433`; container uses service DNS; confirm container health and port mapping |
| `The certificate chain was issued by an authority that is not trusted` | SQL Server encryption certificate is not trusted locally | Configure trusted development certificate or `TrustServerCertificate=True` for local development only |
| `There is already an object named ...` | Schema was created outside the current migration history or history is inconsistent | Compare actual schema with `__EFMigrationsHistory`; do not blindly drop tables |
| `PendingModelChangesWarning` / model differs from snapshot | Model changed without a migration, or model configuration is nondeterministic | Add a migration; remove runtime-random model configuration; inspect snapshot |
| `The entity type ... requires a primary key` | EF discovered a type as an entity without a key, or key configuration is missing | Configure a key, mark keyless query types with `HasNoKey()`, or exclude it from the model |
| FK constraint violation | Referenced principal does not exist, key conversion/type is wrong, or write ordering/relationship is incorrect | Check principal record, FK mapping, generated SQL, and transaction |
| `The instance ... cannot be tracked because another instance with the same key is already being tracked` | Two object instances with one key are attached to the same context | Query and update the tracked object, or detach/use a fresh context appropriately |
| `The LINQ expression could not be translated` | Query contains unsupported client-side method/expression | Rewrite to translatable expressions or intentionally materialize a bounded result before client processing |
| Unexpected extra SQL / N+1 | Lazy loading or repeated queries in loops | Use projection/includes/batching; measure SQL command count |

### SQL Server container readiness

`depends_on` controls container startup order, not necessarily database
readiness. The API or migration command may connect before SQL Server is ready.
Use a health check/retry strategy appropriate to deployment, and check logs:

```bash
docker compose ps
docker compose logs order-db
```

Avoid printing connection strings or passwords in diagnostic logs.

### Data-loss warning

Dropping a database, deleting a Docker volume, or generating/applying a
destructive migration can permanently remove data. Inspect the migration and
back up required data before applying such changes. Never use a volume cleanup
as the first response to a login failure.

## 14. Common implementation patterns

### Keep persistence mapping in infrastructure

For this solution's layering:

```text
Ordering.Domain
  - entities, value objects, domain rules
Ordering.Application
  - use cases and persistence abstractions as appropriate
Ordering.Infrastructure
  - EF DbContext, provider, configurations, migrations
Ordering.API
  - composition root, HTTP endpoints, configuration
```

Avoid making domain classes depend on EF Core when configuration can live in
infrastructure. Private setters and constructors can preserve encapsulation
while still allowing EF materialization when mappings are correct.

### One configuration per entity

Keep table names, lengths, precision, indexes, key conversions, and relationships
near the entity configuration. For money/decimal values specify precision:

```csharp
builder.Property(p => p.Price).HasPrecision(18, 2);
```

Choose precision/scale based on business requirements and provider behavior.
Do not rely on provider defaults for important financial columns.

### Auditing

Set audit fields centrally (for example by overriding `SaveChangesAsync` or
using an interceptor) rather than repeating logic in every handler. Use UTC
timestamps and define how user identity is supplied. Ensure audit behavior
does not overwrite values unexpectedly during imports or migrations.

### Resiliency

Provider retry execution strategies can help with transient failures. If using
retries, ensure operations are safe to repeat and understand transaction
behavior. A retried operation must not duplicate an external side effect.

## 15. Fast workflow for a schema change

1. Update the domain model and EF configuration.
2. Build the solution to catch compile/configuration errors.
3. Add a named migration with explicit target and startup projects.
4. Review `Up`, `Down`, and the model snapshot.
5. Generate/inspect SQL for significant or production changes.
6. Apply locally and test the affected use case against the real provider.
7. Commit the migration files alongside the model/configuration change.
8. Deploy using a reviewed migration script or a controlled migration step.
9. Monitor migration and application health; have a recovery plan.

For example, adding a required `Description` column to an existing populated
table may fail if existing rows have no value. Use a staged migration:
add it nullable (or with a valid default), backfill existing rows, then make it
required in a later migration if needed.

## 16. Local Ordering command examples

From `Services/Ordering/Ordering.Infrastructure`:

```bash
# Inspect the context/provider/configuration EF will use.
dotnet ef dbcontext info \
  --project Ordering.Infrastructure.csproj \
  --startup-project ../Ordering.API/Ordering.API.csproj \
  --context ApplicationDbContext

# Create a migration after changing the model.
dotnet ef migrations add AddOrderDescription \
  --output-dir Data/Migrations \
  --project Ordering.Infrastructure.csproj \
  --startup-project ../Ordering.API/Ordering.API.csproj \
  --context ApplicationDbContext

# Generate SQL for review.
dotnet ef migrations script --idempotent \
  --project Ordering.Infrastructure.csproj \
  --startup-project ../Ordering.API/Ordering.API.csproj \
  --context ApplicationDbContext

# Apply migrations to the configured OrderDb.
dotnet ef database update \
  --project Ordering.Infrastructure.csproj \
  --startup-project ../Ordering.API/Ordering.API.csproj \
  --context ApplicationDbContext
```

The startup project determines how configuration is loaded. If the command
cannot connect, verify the selected environment, connection-string key,
credentials, database host/port, SQL Server readiness, and permissions before
changing or deleting database state.

## 17. Final quick-reference checklist

- [ ] Provider package matches the configured provider.
- [ ] EF runtime, design/tools packages, and CLI tool versions are compatible.
- [ ] Startup project and target project are explicitly selected.
- [ ] Connection string comes from a secret/configuration source, not committed
      credentials.
- [ ] Database host is correct for the caller (host process versus container).
- [ ] Relationships point to entity types; scalar value objects use converters.
- [ ] Required keys, lengths, precision, indexes, and delete behavior are
      explicitly considered.
- [ ] Read-only queries avoid unnecessary tracking and entity graph loading.
- [ ] Migrations are reviewed and committed; production changes are deployed
      deliberately.
- [ ] Concurrency, transactions, retries, and external side effects have
      explicit policies.
- [ ] No production data is deleted to work around configuration problems.

