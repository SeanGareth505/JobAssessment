# Order Management — SADC

Order Management for customers in SADC (Southern African Development Community): Customers and Orders with OrderLineItems; status flow Pending → Paid → Fulfilled → Cancelled. **OrderCreated** is written atomically with the Order to an **Outbox** table in one transaction; a background process publishes from the Outbox to RabbitMQ. The Worker consumes `order-created` (sets order to Fulfilled) and `customer-created` (logs event).

## Tech stack / versions

- **Backend**: ASP.NET Core (.NET 10), EF Core 10 (code-first, SQL Server), RabbitMQ.Client 6.8
- **Frontend**: Angular 21, Angular Material, TypeScript
- **Worker**: .NET 10 Worker, consumes `order-created` and `customer-created` queues
- **Database**: SQL Server (LocalDB or container)
- **Messaging**: RabbitMQ (durable queues). Outbox pattern for OrderCreated: atomic insert of Order + Outbox row; background publisher sends to RabbitMQ and marks rows processed.

## Prerequisites

- .NET SDK 10 (or 8+)
- Node.js 18+ and npm
- SQL Server or LocalDB
- RabbitMQ (for order processing)

## Local vs Docker

- **Local (Backend + Frontend on your machine)**: Backend and Worker are configured to use the **Docker** database and RabbitMQ. Start the DB (and optionally RabbitMQ) in Docker first, then run Backend/Worker/Frontend locally:
  ```bash
  docker compose up -d sqlserver rabbitmq   # start only DB + RabbitMQ
  cd Backend && dotnet run
  cd Worker && dotnet run
  cd Frontend && npm start
  ```
  Connection: **SQL Server** at `localhost,1433` (sa / YourStrong@Passw0rd), **RabbitMQ** at `localhost:5672`.
- **Full Docker**: Run everything in containers: `docker compose up --build`. API and Worker then use container hostnames (e.g. `Server=sqlserver`).

So when you run Backend/Frontend locally, they look for the Docker DB and RabbitMQ; ensure those containers are running.

## Quick start (local)

### Backend

```bash
cd Backend
dotnet restore
dotnet ef database update   # apply migrations (or app does this on startup)
dotnet run
```

API: http://localhost:5018 (or see launchSettings.json). Swagger in Development: /swagger.

### Worker

```bash
cd Worker
dotnet run
```

Consumes from `order-created` and `customer-created` (durable). Ensure RabbitMQ is running.

### Frontend

```bash
cd Frontend
npm install
npm start
```

App: **http://localhost:4200**. Open the app at this URL so API requests go through the dev server proxy; the proxy forwards the `Authorization` header to the backend. If you open the app at the backend URL (e.g. https://localhost:7130) instead, the browser may not send the token and you’ll get 401 on protected routes. Proxy target is set in `Frontend/proxy.conf.js` (default `https://localhost:7130`; override with `API_PROXY_TARGET`).

### Docker Compose (full stack)

From **repository root**:

```bash
docker compose up --build
```

- **Web**: http://localhost:4200
- **API**: http://localhost:8080
- **SQL Server**: localhost:1433 (sa / YourStrong@Passw0rd)
- **RabbitMQ**: AMQP 5672, Management http://localhost:15672 (guest/guest)

API runs migrations on startup (`MigrateAsync` with retries). Worker consumes `order-created` (sets order to Fulfilled) and `customer-created` (logs event).

## EF Core migrations and database lifecycle

### Migration layout

Migrations follow the required structure:

- **InitialCreate** (`20260216000000_InitialCreate`): Customers, Orders, OrderLineItems with indexes and foreign keys. No RowVersion or Outbox.
- **AddRowVersionAndOutbox** (`20260216000001_AddRowVersionAndOutbox`): Adds `rowversion` to Orders and creates the OutboxMessages table.
- **AddProductsTable** (`20260216000002_AddProductsTable`): Products table with unique Sku.
- **LinkOrderLineItemToProduct** (`20260216000003_LinkOrderLineItemToProduct`): ProductId FK on OrderLineItems.
- **AddStoredProcedureTransactionReport** (`20260216000004_AddStoredProcedureTransactionReport`): Stored procedure `sp_GetTransactionReport`.

### Migration commands

From `Backend` folder:

```bash
# Apply migrations (create/update database)
dotnet ef database update

# Add a new migration after model changes
dotnet ef migrations add YourMigrationName --output-dir Migrations

# Generate SQL script (for operational review or manual apply)
dotnet ef migrations script --output migration.sql
# Idempotent script (for applying to existing DB): add --idempotent
dotnet ef migrations script --idempotent --output migration_idempotent.sql
# The generated script is not committed; CI verifies it compiles (dotnet ef migrations script).

# Rollback to a previous migration
dotnet ef database update PreviousMigrationName
```

### Zero-downtime strategy

1. **Additive first**: Prefer adding nullable columns or new tables; avoid dropping columns or renaming in the same release as app code that still references the old schema.
2. **Deploy order**: Apply migration (e.g. during maintenance or with backward-compatible schema), then deploy new application version. For blue/green: deploy new app to green, run migration, switch traffic.
3. **Backfills**: For new non-nullable columns, add column as nullable with default, deploy app that can handle null, run a backfill job (batch, capped size), then add NOT NULL constraint in a follow-up migration.
4. **Long-running backfills**: Run outside the request path (background job or script); cap batch size to avoid long locks.

### Rollback

- **Revert app**: Redeploy previous app version.
- **Revert database**: `dotnet ef database update <PreviousMigrationName>`. If the migration you are reverting did irreversible data changes (e.g. column drop), restore from backup first.

## SADC and Common Monetary Area (CMA)

SADC country codes (ISO 3166-1 alpha-2) and allowed currencies (ISO 4217) are validated in the API (see `SadcValidationService`). Zimbabwe (ZW) allows ZWL and USD. The Common Monetary Area (ZAR, NAD, LSL, SZL) implications (e.g. at-par currencies) can be documented in architecture notes; the current implementation validates country–currency pairing per the supported table.

## Authentication and security

- **Mock auth** is for local development only. Login uses `Auth:MockPassword` and JWT uses `Jwt:Key` from configuration; both are set in `appsettings.Development.json` only (not in base `appsettings.json`).
- **Production / Microsoft Entra**: For production with **Microsoft Entra ID**, use **Microsoft.Identity.Web**: add `AddMicrosoftIdentityWebApi()` and configure tenant, client ID, and audience. This solution uses custom JWT validation (`AddJwtBearer`) and a mock login endpoint so it runs without an Entra tenant; replace with Entra-backed login and token validation (e.g. `Microsoft.Identity.Web`) for production.
- **Password comparison** uses constant-time comparison (`CryptographicOperations.FixedTimeEquals`) to reduce timing-attack risk.
- **CORS** is restricted to origins listed in `Cors:AllowedOrigins` (default `http://localhost:4200`).
- **JWT**: Validated with issuer, audience, lifetime, and signing key; no default key in code. Token is sent in `Authorization: Bearer` only; frontend stores it in `sessionStorage` and does not send it to the login endpoint.

## Data model

Main entities:

| Entity            | Table            | Alignment                                                                                                                                                                  |
| ----------------- | ---------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Customer**      | `Customers`      | ✓ Id, Name, Email, CountryCode, CreatedAt                                                                                                                                  |
| **Order**         | `Orders`         | ✓ Id, CustomerId, Status, CreatedAt, CurrencyCode, TotalAmount; `RowVersion` for concurrency                                                                               |
| **OrderLineItem** | `OrderLineItems` | ✓ Id, OrderId, ProductSku (string), Quantity, UnitPrice; optional ProductId FK.                                             |
| **Products**      | `Products`       | Id, Sku, Name, CreatedAt. Used by the UI; line items store ProductSku as string. |
| **Outbox**        | `OutboxMessages` | ✓ Outbox for reliable messaging                                                                                                                                            |

**Naming:** OrderLineItems are the line items on an order. Products is the catalog (SKU + name). Line items store ProductSku as a string and may optionally reference Products via ProductId.

## API summary

- **Customers**: POST /api/customers (create, publish CustomerCreated), GET /api/customers?search=&page=&pageSize=, GET /api/customers/{id}, PUT /api/customers/{id}
- **Orders**: POST /api/orders (validate customer + currency, compute total; **Order + Outbox row saved in one transaction**; background process publishes OrderCreated to RabbitMQ), GET /api/orders?customerId=&status=&page=&pageSize=&sort=, GET /api/orders/{id} (ETag), PUT /api/orders/{id} (line items when Pending), PUT /api/orders/{id}/status (Idempotency-Key)
- **Products**: GET /api/products?search=&page=&pageSize=, POST /api/products (create product for catalog)
- **Health**: GET /healthz (liveness), GET /readiness (DB)
- **Metrics**: Request count and request duration are recorded via `System.Diagnostics.Metrics` (Meter: `Backend.Api`).
- **Stored procedure**: `sp_GetTransactionReport` — see migration `AddStoredProcedureTransactionReport`; parameters `@StartDate`, `@EndDate`, `@Status` (INT), `@CustomerId`; returns detail rows and summary (TotalOrders, GrandTotalAmount).

## Postman / .http

See `Backend/Backend.http` for example requests (customers, orders, status update with Idempotency-Key).

## Seed data

On **first run in Development** (or when `SeedData:RunOnStartup` is true), the API runs `SeedData.SeedIfEmptyAsync`: it inserts products, customers for all SADC countries, and **≥1,000 orders** with valid country/currency pairing and OrderCreated outbox rows. Idempotent: skipped if orders already exist.
