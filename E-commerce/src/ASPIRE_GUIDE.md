# .NET Aspire adoption guide

This guide proposes how to add .NET Aspire to this repository for local
development. It describes the changes to make; Aspire has **not** been added
to the solution yet.

The goal is to use one .NET AppHost to start the APIs, gateway, and development
dependencies, while gaining a dashboard for logs, traces, health, and resource
status. Keep the existing Dockerfiles and `compose.yaml` for container-based
workflows and deployment experiments. Aspire is a development orchestrator and
does not replace production deployment, database migration planning, or secret
management.

## 1. What the solution contains today

The projects target .NET 10 and the service topology is:

| Runnable project | Role | Current dependencies |
|---|---|---|
| `Services/Catalog/CatalogAPI` | HTTP catalog API; uses Carter, MediatR, and Marten | PostgreSQL service `catalog-db`, database `CatalogDb` |
| `Services/Basket/BasketAPI/BasketAPI` | HTTP basket API; uses Marten and Redis | PostgreSQL `basket-db` / `BasketDb`, Redis, Discount gRPC, RabbitMQ |
| `Services/Discount/Discount.gRPC` | Discount gRPC service; uses EF Core SQLite | Local SQLite file `discountDb.db` |
| `Services/Ordering/Ordering.API` | HTTP ordering API; uses EF Core SQL Server and MassTransit | SQL Server `order-db` / `OrderDb`, RabbitMQ |
| `ApiGateways/YarpApiGateway` | YARP reverse proxy | Catalog, Basket, and Ordering APIs |

`BuildingBlocks`, `BuildingBlocks.Messaging`, and the Ordering
`Application`/`Domain`/`Infrastructure` projects are libraries, not standalone
processes. Do not register them as AppHost projects; their containing APIs
bring them into the process.

The current connection-setting names that application code reads are:

| Application | Configuration keys read by the code |
|---|---|
| Catalog | `ConnectionStrings:Database` |
| Basket | `ConnectionStrings:Database`, `ConnectionStrings:Redis`, `GrpcSettings:DiscountUrl`, and `MessageBroker:Host/UserName/Password` |
| Discount | `ConnectionStrings:Database` |
| Ordering | `ConnectionStrings:Database` and `MessageBroker:Host/UserName/Password` |
| Gateway | `ReverseProxy` |

Preserving these names at the Aspire boundary avoids unnecessary changes to
the existing data-access code. RabbitMQ and service-to-service discovery need
some deliberate integration work because the current applications use
hand-written host URLs and separate broker settings.

## 2. Recommended target architecture

Add two projects beside the existing projects:

- **`ECommerce.AppHost`** declares the processes, containers, references, and
  startup dependencies for local development.
- **`ECommerce.ServiceDefaults`** holds shared ASP.NET service defaults:
  OpenTelemetry, health endpoints, and service discovery setup.

The AppHost should start the five runnable projects and these development
resources:

```text
ECommerce.AppHost
├── catalog-api ─────── catalog PostgreSQL database
├── basket-api ──────── basket PostgreSQL database
│   ├────────────────── Redis
│   ├────────────────── RabbitMQ
│   └────────────────── discount-gRPC
├── discount-gRPC ──── local SQLite file (initially)
├── ordering-api ───── SQL Server database
│   └────────────────── RabbitMQ
└── yarp-api-gateway ─ catalog-api, basket-api, ordering-api
```

Run the AppHost rather than running Compose and the same APIs at the same time.
Both orchestrators would otherwise compete for host ports and can create two
separate copies of each database and broker.

## 3. Create the Aspire projects

Use Aspire templates compatible with the repository's .NET 10 SDK. If the
templates are not installed, install the current stable Aspire project
templates, then confirm the available template names with `dotnet new list
aspire`. Create the AppHost and ServiceDefaults projects in `E-commerce/src`,
and add them to `E-commerce.sln`.

For example, with the template names exposed by the installed SDK:

```bash
cd E-commerce/src
dotnet new aspire-apphost -n ECommerce.AppHost
dotnet new aspire-servicedefaults -n ECommerce.ServiceDefaults
dotnet sln E-commerce.sln add ECommerce.AppHost/ECommerce.AppHost.csproj
dotnet sln E-commerce.sln add ECommerce.ServiceDefaults/ECommerce.ServiceDefaults.csproj
```

If the installed SDK uses different Aspire template names, use the names shown
by `dotnet new list aspire`; do not install a random preview or mix versions.
Keep all Aspire packages on one stable version compatible with .NET 10.

Add project references from ServiceDefaults to each **web application**:

- `CatalogAPI.csproj`
- `BasketAPI.csproj`
- `Discount.gRPC.csproj`
- `Ordering.API.csproj`
- `YarpApiGateway.csproj`

Add project references from AppHost to those same five projects so it can use
the project resources. The generated `Projects.*` type names come from the
referenced project names; inspect the generated AppHost project or use IDE
completion rather than assuming that punctuation in names such as
`Discount.gRPC` becomes a particular identifier.

Keep Aspire package versions centralized/consistent if the solution adopts
Central Package Management. Do not upgrade existing EF Core, MassTransit, or
other unrelated packages as part of the first Aspire change.

## 4. Declare infrastructure in the AppHost

Add the provider packages required by the AppHost template for PostgreSQL,
Redis, RabbitMQ, and SQL Server. Define one resource per shared local
dependency; use resource references rather than hard-coded host ports.

A structural example (project type identifiers need to match those generated
by the AppHost project) is:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume();
var catalogDb = postgres.AddDatabase("catalogdb", "CatalogDb");
var basketDb = postgres.AddDatabase("basketdb", "BasketDb");

var redis = builder.AddRedis("redis");
var rabbit = builder.AddRabbitMQ("rabbitmq");

var sqlPassword = builder.AddParameter("sql-password", secret: true);
var sqlServer = builder.AddSqlServer("sqlserver", sqlPassword)
    .WithDataVolume();
var orderDb = sqlServer.AddDatabase("orderdb", "OrderDb");

var discount = builder.AddProject<Projects.DiscountGrpc>("discount-grpc");
var catalog = builder.AddProject<Projects.CatalogAPI>("catalog-api");
var basket = builder.AddProject<Projects.BasketAPI>("basket-api");
var ordering = builder.AddProject<Projects.OrderingAPI>("ordering-api");
var gateway = builder.AddProject<Projects.YarpApiGateway>("yarp-api-gateway");

// Add resource references and WaitFor dependencies as described below.

builder.Build().Run();
```

This shows the intent, not a guaranteed copy/paste file: use the actual
`Projects.*` identifiers and the API overloads provided by the installed
Aspire package version.

### Why these resources

- **PostgreSQL:** Catalog and Basket already use PostgreSQL through Marten.
  Keep them as separate databases so Aspire reflects the existing service
  ownership boundary and their data remains isolated.
- **Redis:** Basket uses Redis for distributed caching. Aspire can provide its
  connection information and dashboard lifecycle without relying on a
  machine-specific port.
- **RabbitMQ:** Basket and Ordering communicate through MassTransit. Run one
  broker resource and reference it from both services.
- **SQL Server:** Ordering uses the SQL Server EF Core provider and migrations.
  Keep the provider and schema unchanged while introducing orchestration.
- **Discount SQLite:** SQLite is a file database, not a shared network
  service. Keep it file-backed for the first Aspire pass. Moving it to
  PostgreSQL would change provider behavior and migrations, so evaluate that
  separately.
- **Data volumes:** Volumes preserve local database state between AppHost
  runs. Provide an explicit documented reset procedure for developers; do not
  routinely delete volumes as a way to fix startup issues.

SQL Server needs a secret password satisfying SQL Server's password policy.
Supply it through the AppHost's local user-secrets/parameter mechanism, not
source control. Do not copy the development password from Compose into
AppHost source. Review SQL Server container image architecture support on
Apple Silicon and use a compatible image/platform for the local Docker
environment.

## 5. Connect each service

### Catalog API

1. Add the ServiceDefaults project reference.
2. Call `builder.AddServiceDefaults()` near the start of `Program.cs`.
3. Retain the existing Marten registration and `ConnectionStrings:Database`
   lookup.
4. In AppHost, reference `catalogDb` using the connection name `Database` and
   make Catalog wait for that database resource to be ready.
5. Map the standard Aspire health endpoints, but do not leave two different
   handlers mapped to the same `/health` path. Catalog already registers
   health checks and maps `/health`; either adopt the ServiceDefaults mapping
   consistently or retain the application-specific mapping and adjust the
   defaults accordingly.

**Reason:** This lets Aspire supply the PostgreSQL address dynamically while
Catalog keeps using its existing database configuration contract.

### Basket API

1. Add ServiceDefaults and keep the existing Marten, Redis cache, health
   checks, and MassTransit registrations.
2. Reference `basketDb` with connection name `Database`, Redis with connection
   name `Redis`, RabbitMQ with the broker connection name agreed for the
   application, and Discount as a service reference.
3. Wait for the Basket database, Redis, and RabbitMQ resources. Wait for
   Discount if Basket should not be considered ready until the gRPC dependency
   is available; otherwise let the resilient client handle temporary
   unavailability.
4. Change the Discount URL from the current `https://localhost:5052` /
   Compose-only hostname to the Aspire service-discovery name and endpoint.
   Configure the gRPC client for the protocol used by Discount, and remove the
   development-only “accept any server certificate” callback when using
   internal HTTP/2 in local Aspire. Do not disable certificate validation in
   production.
5. Continue exposing the current database and Redis health checks. Ensure
   there is only one `/health` mapping after adding ServiceDefaults.

**Reason:** Basket is the service with the most dependencies. Resource
references eliminate hard-coded localhost/Compose URLs, while health checks
and traces make dependency failures easier to diagnose.

### Discount gRPC

1. Add ServiceDefaults, preserving `AddGrpc()` and the current gRPC service
   registration.
2. Keep SQLite and `ConnectionStrings:Database` for the first migration.
   Confirm the SQLite file is written to a stable, expected working directory
   when launched by AppHost; use an explicit local data path if the default
   relative path produces a file in an unexpected directory.
3. Preserve HTTP/2 on the gRPC endpoint. A normal JSON/HTTP health endpoint
   and gRPC endpoint have different protocol expectations; do not change the
   gRPC listener to HTTP/1.1 only.
4. Expose health/readiness in a way that does not interfere with the HTTP/2
   gRPC listener. Verify a real gRPC call from Basket, not only that the
   process appears in the dashboard.

**Reason:** Keeping SQLite avoids combining orchestration work with a data
provider migration. Explicitly validating HTTP/2 catches a common
service-discovery/gRPC integration issue.

### Ordering API

1. Add ServiceDefaults; keep the SQL Server EF Core provider and
   `ConnectionStrings:Database`.
2. Reference the SQL Server `orderDb` resource with connection name
   `Database`; wait for the database resource before startup migrations run.
3. Reference RabbitMQ and wait for the broker if the MassTransit bus must be
   connected before the API starts serving traffic.
4. Preserve the current development-only migration/seed behavior initially.
   AppHost waiting for a database means the server is ready, not that
   migrations have succeeded; migration errors should still fail startup
   visibly.
5. Add a health endpoint and ensure the SQL readiness check reflects whether
   the API can actually serve database-backed requests.

**Reason:** This preserves the working SQL Server connection and startup
migrations while making dependency order explicit. Aspire orchestration does
not replace a reviewed production migration process.

### YARP API Gateway

1. Add ServiceDefaults and use service discovery for proxy destinations.
2. Add AppHost references to Catalog, Basket, and Ordering so their endpoints
   are discoverable.
3. Replace the `localhost` HTTPS destination addresses in
   `ApiGateways/YarpApiGateway/appsettings.json` with internal service names
   and the correct scheme/endpoint for each API. Add the YARP service
   discovery destination resolver supported by the selected YARP/Aspire
   package versions.
4. Keep the existing route matches and `PathPattern` transforms unless the
   public URL design is intentionally changing. The transform strips the
   service prefix and forwards the captured remainder of the path.
5. Check the existing Ordering destination carefully: it currently points to
   `https://localhost:6062`, which is the host port used for Discount in
   Compose, not Ordering (`6063`). Service discovery removes this host-port
   coupling and avoids the mismatch.
6. Configure gateway readiness to report unavailable if required backends
   cannot be reached, or document that the gateway can start before backends
   while returning proxy errors until they are ready.

**Reason:** `localhost` inside the gateway process means the gateway itself.
Aspire service discovery routes to the other project endpoints and permits
ports to change without editing the proxy configuration.

## 6. Standardize RabbitMQ configuration

The shared `BuildingBlocks.Messaging/MassTransit/Extension.cs` currently reads
three independent values:

```text
MessageBroker:Host
MessageBroker:UserName
MessageBroker:Password
```

Aspire's RabbitMQ resource naturally provides a connection string. Choose one
configuration contract and use it in both Basket and Ordering. Recommended:

1. Have AppHost reference RabbitMQ using the same connection-string key (for
   example, `MessageBroker`) in both projects.
2. Update the shared `AddMessageBroker` extension to read
   `GetConnectionString("MessageBroker")` and configure MassTransit with that
   full broker URI.
3. Keep development broker credentials out of committed JSON. Use local
   Aspire parameters/secrets where credentials must be overridden.
4. If retaining the existing three-key configuration instead, AppHost must
   explicitly provide all three values consistently; do not assume a RabbitMQ
   resource reference automatically populates `MessageBroker:Host`.

Updating the shared extension once avoids different broker behavior in Basket
and Ordering. Verify the exact connection-string format emitted by the chosen
Aspire RabbitMQ integration before changing the registration.

## 7. Add shared ServiceDefaults

Use the generated ServiceDefaults template as the baseline, then apply it to
the five web projects. It should centralize:

- OpenTelemetry logging, metrics, and distributed tracing.
- ASP.NET Core and outbound HTTP instrumentation.
- Service discovery for `HttpClient`-based calls.
- Standard liveness/readiness health endpoints.
- Resilience defaults where appropriate.

At a high level, each web application's `Program.cs` should follow this
pattern:

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// Existing service registrations stay here.

var app = builder.Build();

// Existing middleware and route mappings stay here.
app.MapDefaultEndpoints();
app.Run();
```

Merge this with each existing `Program.cs`; do not replace the service's
registrations, middleware, Carter mappings, gRPC mapping, YARP mapping, or
database initialization with the abbreviated example. If an API already owns
`/health`, consolidate the mappings instead of registering duplicate paths.
Keep health checks useful and safe; do not expose sensitive diagnostic details
on a public production endpoint.

ServiceDefaults should not force every project to use the same database,
logging levels, or API routes. It is for cross-cutting operational defaults,
not business-specific infrastructure.

## 8. Run and verify in stages

Work incrementally so a failure can be attributed to one integration:

1. **Resources only:** Start PostgreSQL, Redis, RabbitMQ, and SQL Server from
   AppHost. Confirm they become healthy and the dashboard displays them.
2. **One API at a time:** Add Catalog and verify its `/health` endpoint and a
   database-backed product request. Repeat for Basket, Discount, and
   Ordering.
3. **Messaging:** Verify Basket and Ordering connect to the same RabbitMQ
   resource; exercise the basket-checkout event and confirm the Ordering
   consumer handles it.
4. **gRPC:** Exercise Basket's Discount call and verify HTTP/2, discovery, and
   certificate handling.
5. **Gateway:** Start YARP last, verify each prefix route, and confirm the
   path transform forwards the expected path.
6. **Observability:** Confirm traces cross Basket → Discount and
   Basket → RabbitMQ → Ordering where instrumentation supports it. Check
   service logs and health in the Aspire dashboard.
7. **Container workflow:** Run the existing Compose workflow separately to
   ensure Dockerfiles and container configuration remain usable.

The exact command depends on the generated AppHost project name, normally:

```bash
dotnet run --project ECommerce.AppHost
```

For repeatable checks, use the dashboard's resource health and logs first,
then call the existing API and gRPC endpoints. Do not treat “AppHost started”
as proof that database migrations, message consumption, or proxy routes work.

## 9. Migration and operational cautions

- **Do not run Aspire and Compose together for the same stack.** Stop one
  orchestrator before starting the other.
- **Do not move production secrets into AppHost code or committed
  `appsettings.json`.** Aspire parameters can be locally secret-backed; use
  the deployment platform's secret store outside development.
- **Do not migrate Discount from SQLite during this work.** A provider change
  needs its own schema/data migration and test plan.
- **Do not assume resource readiness means schema readiness.** Keep migration
  failures visible and use readiness checks that reflect application needs.
- **Keep production migrations deliberate.** Development startup migrations
  are convenient locally but are not a substitute for a controlled deployment
  migration step, especially with multiple replicas.
- **Treat generated endpoints as internal addresses.** Use service discovery
  or resource references instead of host-published ports and `localhost`.
- **Keep gRPC on HTTP/2.** Verify endpoint protocol explicitly after adopting
  service discovery or changing local HTTPS settings.
- **Persist state intentionally.** Volumes retain local database data; SQLite
  persists only if its file path points to a durable location.
- **Keep Aspire dependencies aligned.** Use compatible stable Aspire hosting
  and service-default packages; avoid mixing preview package versions with
  stable .NET 10 application packages.

## 10. Completion checklist

- [ ] AppHost and ServiceDefaults are in `E-commerce.sln`.
- [ ] AppHost references all five runnable projects, not the class libraries.
- [ ] PostgreSQL Catalog/Basket, Redis, RabbitMQ, and SQL Server start as
      Aspire resources.
- [ ] Local SQL Server credentials are secret-backed, not committed.
- [ ] Catalog and Basket retain their separate PostgreSQL databases.
- [ ] Discount still uses SQLite unless a separate provider-migration plan is
      approved.
- [ ] Database references populate the existing `ConnectionStrings:Database`
      contract.
- [ ] Basket Redis and both MassTransit clients receive valid Aspire
      connection information.
- [ ] Basket resolves Discount by service name and completes an HTTP/2 gRPC
      call.
- [ ] YARP resolves Catalog, Basket, and Ordering using service discovery;
      routes and path transforms still behave as intended.
- [ ] Each API exposes non-duplicated health endpoints and useful readiness
      checks.
- [ ] Dashboard logs, traces, and health are visible for all applications.
- [ ] Existing Compose/Docker workflows still work when run separately.
