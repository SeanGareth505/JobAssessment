# Q&A — Tech stack and SQL

Notes on the general tech questions and the SQL section.

---

## General tech stack

### 1. Async/await in ASP.NET Core for I/O-bound work. When would you use Task.Run in a web API?

Use async/await for anything I/O-bound (DB, HTTP, files, RabbitMQ) so you’re not blocking threads. Avoid async void; use async Task. In library code I’d use ConfigureAwait(false); in app code it’s often left out. Pass CancellationToken through to I/O calls.

Task.Run in a web API only when you really need to push CPU-bound work off the request thread (e.g. heavy number crunching). Don’t wrap async I/O in Task.Run—it just adds a thread hop and no benefit.

### 2. Minimal APIs vs controller-based — when do you prefer each?

Controllers give you a clear structure: actions, filters, [Authorize], and Swagger plays nicely with them. Minimal APIs are good when you have a tiny surface (a few endpoints) and want less boilerplate; they get messy once you add auth, validation, and docs in one place.

I’d pick controllers for a full API so the code stays easy to follow and test; minimal APIs for small services or internal tools with few routes.

### 3. EF Core: tracking vs AsNoTracking. How do you do optimistic concurrency safely?

With tracking, EF keeps entities in the change tracker and updates apply to those instances—use that when you’re doing read-modify-write in one request. AsNoTracking skips the tracker: read-only, faster, no accidental updates. Use it for lists and lookups where you only read.

For optimistic concurrency, add a concurrency token (e.g. rowversion) on the entity. On update, EF puts it in the WHERE clause. If someone else changed the row, no rows update and you get DbUpdateConcurrencyException. Handle it by reloading and returning 409 with the current state (e.g. problem details).

### 4. Where do you enforce business rules (DTO vs domain vs DB)? FluentValidation vs manual?

DTOs for input shape—required, length, range. Domain or service layer for business rules (e.g. SADC country–currency, status transitions). DB constraints (FKs, unique, check) as a last line of defence.

I use data annotations for simple DTO rules. For trickier stuff (e.g. currency per country), either FluentValidation or manual checks in the service. FluentValidation is nice when you have a lot of rules and want them in one place with async and cross-property support; manual is fine for a small set.

### 5. Exception handling: global handlers and what error responses should look like

Use a single place (middleware or filter) that catches unhandled exceptions and returns JSON (e.g. application/problem+json) with type, title, status, detail, instance (request path), and optionally errors for validation. Stick to consistent status codes (400, 404, 409, 500). For validation failures use ValidationProblemDetails and ModelState. Don’t expose stack traces or internals in production.

### 6. REST search, pagination, sorting; when is GraphQL a better fit; N+1

Query params for filters (search, customerId, status), page and pageSize (cap pageSize), and sort (e.g. createdAt, -totalAmount). Return something like items, totalCount, page, pageSize.

GraphQL makes sense when clients need lots of different shapes and nested data in one shot, or when over/under-fetching is a real pain. For a straightforward REST API with a few endpoints and DTOs, I wouldn’t bother.

N+1: use Include/ThenInclude or explicit load for what you need, or project with Select in one query. AsNoTracking for read-only lists.

### 7. Validating JWT (e.g. with Entra), roles, token lifetime, policy-based auth

AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer() and set TokenValidationParameters (issuer, audience, signing key). For production with Entra I’d use Microsoft.Identity.Web (AddMicrosoftIdentityWebApi) and wire tenant, client ID, audience. Map roles from the token claims and use AddAuthorization() with RequireRole("Orders.Read") etc., then [Authorize(Policy = "Orders.Read")] on controllers. Token lifetime is set at the identity provider; short-lived access tokens and refresh tokens for longer sessions.

### 8. RabbitMQ: exchange types, durability, at-least-once, idempotency. When to use the outbox pattern?

Direct (routing key), topic (pattern), fanout (broadcast). Durable queues survive broker restart. For at-least-once: publish with confirmations; consumer acks only after processing. Idempotency: consumer checks “already handled” (e.g. by aggregate id and status) and acks without re-applying.

Outbox: when you can’t afford to commit in the DB and then fail to publish. Write the event to an Outbox table in the same transaction as the domain write; a separate process polls the Outbox, publishes to RabbitMQ, then marks the row processed. That way you never have “saved but not published.”

### 9. Frontend: state management and keeping API contracts type-safe

The brief suggested React; this solution uses **Angular** (my stack for this take-home). The ideas are the same either way.

**In Angular (what’s in this repo):** State lives in injectable services (root or route-scoped); signals give reactive updates. For bigger apps, NgRx is an option. One API service uses `HttpClient` with typed DTOs and returns `Observable<T>`. An HTTP interceptor attaches the JWT and handles errors so the rest of the app only sees typed responses.

**In React you’d do the equivalent:** Shared state via Context for simple cases, or Redux / Zustand when it grows. Server state with SWR, React Query, or a small fetch wrapper. Same type safety: TypeScript interfaces matching the API, a single API layer (fetch or axios) that returns typed Promises. Auth and errors: axios interceptors or a wrapper that adds the token and maps failed responses. So it’s not a different approach—just different primitives (services + DI + observables vs hooks + context/store + promises). I’m comfortable in both; for this project I went with Angular.

### 10. Test pyramid, unit vs integration vs E2E, what to run in CI

Lots of unit tests (domain, validation, mapping), fewer integration (API + DB, messaging), a few E2E for critical flows.

Unit: fast, no I/O—domain rules, validation, status transitions. Integration: WebApplicationFactory, real or test DB, hit endpoints and assert. E2E: e.g. Playwright/Cypress for “create customer → create order → change status.”

In CI I’d run restore, build, lint, unit tests, integration (optionally with test containers), and migration script verification. Coverage and security scans if you have time.

### 11. Write spikes, indexing, caching, correlation IDs, SLOs

Write spikes: queue (e.g. RabbitMQ) to decouple; backpressure and rate limiting at the API. Indexes on what you filter and sort on (e.g. CustomerId, Status, CreatedAt); covering index if listing is hot. Caching: ETag/If-None-Match for GET by id; short-lived cache for lists if that’s acceptable. Correlation ID: middleware that sets or reads X-Correlation-ID, puts it in log scope and response header so you can trace a request across API and workers. SLOs: define latency and error targets, emit metrics (request duration, counts), alert when you’re at risk.

---

## SQL (SQL Server)

### 1. Pagination — customer’s orders with a total count

One option: two statements (or two result sets)—one with OFFSET/FETCH for the page, one with COUNT(*). Or use a window: COUNT(*) OVER () AS TotalCount.

### 2. Top 10 spenders in the last 90 days (include customers with zero orders)

LEFT JOIN Customers to Orders with the date filter in the JOIN (so customers with no orders still show up). GROUP BY customer, COALESCE(SUM(TotalAmount), 0), ORDER BY that sum DESC, TOP 10.

### 3. Index for (CustomerId, Status, CreatedAt)

Create an index on Orders (CustomerId, Status, CreatedAt). Covers filter and sort. If listing is hot you can add TotalAmount/CurrencyCode to the index (covering) to avoid key lookups, at the cost of a bigger index and more write overhead.

### 4. Execution plan and key lookup

Key lookup is when the engine does a seek/scan on a nonclustered index then goes back to the clustered index to get other columns. To get rid of it, add those columns to the nonclustered index (INCLUDE or key columns) so the query is covered.

### 5. Optimistic concurrency with rowversion

Add byte[] RowVersion to the entity and in OnModelCreating: Property(e => e.RowVersion).IsRowVersion(). On update, catch DbUpdateConcurrencyException, reload, return 409 with current state (e.g. problem details).

### 6. Deadlocks — reader/writer scenario and how to reduce them

Classic case: two transactions lock rows in different order (T1: A then B, T2: B then A) and block each other. Mitigation: lock in a consistent order (e.g. always by Id), keep transactions short, consider READ COMMITTED SNAPSHOT to cut blocking. Use locking hints only where you need them.

### 7. Running total per customer (window function)

SUM(TotalAmount) OVER (PARTITION BY CustomerId ORDER BY CreatedAt) gives you the running total per customer. Order by CustomerId, CreatedAt in the outer query.

### 8. Partitioning for very large order tables

Partition by CreatedAt (e.g. month). Partition function + scheme, then partitioned index. To archive: new partition for future data, switch an old partition to a staging table, move to archive, drop the partition.

### 9. Outbox — atomic Order + Outbox insert

One transaction: INSERT Orders …; INSERT OutboxMessages (AggregateType, AggregateId, Type, Payload, …) …; COMMIT. A background job selects where ProcessedAtUtc IS NULL, publishes to RabbitMQ, sets ProcessedAtUtc, commits.

### 10. Stored procedure — Transaction Report

Done in migration AddStoredProcedureTransactionReport: sp_GetTransactionReport with @StartDate, @EndDate, @Status, @CustomerId. First result set: detail rows (customer name, email, country; order id, status, created, currency, total; line items sku, qty, unit price). Second: TotalOrders, GrandTotalAmount. Index (CustomerId, Status, CreatedAt) is there; a covering index with CurrencyCode and TotalAmount can help if listing is heavy.
