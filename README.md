# WeakAppHandler

## Running the stack

```bash
cp .env.example .env
docker compose up -d --build
```

That brings up everything: the third-party API, RabbitMQ, PostgreSQL, pgAdmin and all four services.
`docker compose ps` shows the state of each; the first build takes a few minutes.

If a PostgreSQL volume is left over from an earlier run, wipe it first:

```bash
docker compose down -v && docker compose up -d --build
```

`POSTGRES_USER` and `POSTGRES_PASSWORD` are applied only while the data directory is empty, and
so is `db/init/01-gateway-role.sh`. On an existing volume both are ignored, which shows up as
`28P01: password authentication failed` in the processor log, and as a missing `gateway` role
for the gateway.

| Service | Where |
|---------|-------|
| WeakApp | http://localhost:8080/meters |
| RabbitMQ management UI | http://localhost:15672 (guest/guest) |
| PostgreSQL | localhost:5432 |
| pgAdmin | http://localhost:5050 |
| Data Ingestor | http://localhost:5227 |
| Data Processor | http://localhost:5242 |
| GraphQL Gateway / Nitro IDE | http://localhost:5243/graphql |
| Notification Service / test client | http://localhost:5244 |
| Dashboard | http://localhost:5180 |

Startup order is handled by health checks: the ingestor waits for WeakApp and the broker, the
processor for the broker and the database, the notification service for the broker. The
processor applies the EF migrations when it starts, so the schema appears on its own.

`Cannot load library libgssapi_krb5.so.2` in a service log is noise, not a failure: Npgsql
probes for Kerberos, does not find it in the runtime image, and falls back to password
authentication.

### Running a service from the IDE

Stop the container and run the project — the port is already published for it:

```bash
docker compose stop processor
dotnet run --project Source/DataProcessorService/DataProcessorService.API
```

`appsettings.json` points at `localhost`, and inside the network the containers are reached by
service name, so both ways work without editing configuration. In containers the values come
from the environment (`ConnectionStrings__Database`, `RabbitMq__Host`, `WeakApp__BaseUrl`).

## WeakApp

A vendored third-party API that reports meter readings and fails on purpose — roughly one
response in ten is a 5xx, a rate-limit or a truncated body. See [THIRD_PARTY.md](THIRD_PARTY.md)
for provenance and licence.

| What | Where |
|------|-------|
| Readings | `GET http://localhost:8080/meters` |
| Health | `GET http://localhost:8080/health` |
| OpenAPI document | `GET http://localhost:8080/openapi/v1.json` |

Every request needs the API key header:

```bash
curl -i -H "X-Api-Key: supersecret" http://localhost:8080/meters
```

Without it the API answers `401 Invalid or missing API key`. There is no Swagger UI — the
package the app uses serves the OpenAPI document as JSON only.

From inside the `backend` network the service is reachable as `http://weakapp:8080`.

### How the ingestor survives it

WeakApp allows ten requests per rolling forty-second window. Polling every ten seconds spends
four of those on the happy path, which leaves room for the retries the API's deliberate failures
make necessary.

Server errors, network faults and timeouts are retried twice with exponential backoff and
jitter, so a failing poll costs at most three of the ten. `429` is not retried at all.

That last part is deliberate. WeakApp answers a rate limit with `Retry-After: 1`, and honouring
it inside the poll turns one rejected call into several within a few seconds — precisely when
the server has asked for fewer. Because the window slides, the budget then never refills: every
poll keeps topping it up faster than it drains, and the ingestor serves nothing but `429` until
it is restarted. A poll is idempotent and comes round again on the ten-second tick, which is a
longer wait than the server asks for anyway, so the tick is the backoff and a rate-limited poll
is simply abandoned.

Both numbers live in `WeakApp` configuration, so the budget can be re-cut without a rebuild if
the third-party limits change.

A poll that returns meters whose payloads all fail to parse publishes nothing. Empty batches
would otherwise travel through the broker only to be rejected by the consumer as
`reading_batch.empty`.

## Database roles

Two roles, split by what each service is allowed to do.

| Role | Used by | Rights |
|------|---------|--------|
| `processor` | Data Processor | owns the schema, runs migrations, reads and writes |
| `gateway` | GraphQL Gateway | `SELECT` only |

`db/init/01-gateway-role.sh` creates the second one, on the first start with an empty data
directory. The script grants `SELECT` on existing tables and, through `ALTER DEFAULT
PRIVILEGES`, on every table the `processor` role creates afterwards. That last part matters: at
init time the schema does not exist yet, because migrations run when the processor first starts.

Check it took effect:

```bash
docker compose exec postgres psql -U gateway -d weakapphandler -c 'select count(*) from "Readings"'
docker compose exec postgres psql -U gateway -d weakapphandler -c 'delete from "Readings"'
```

The first succeeds, the second fails with `permission denied for table Readings`.

## REST API

The processor owns the write side, so REST lives there rather than on the read-only gateway.

| What | Where |
|------|-------|
| Endpoints | `http://localhost:5242/api` |
| OpenAPI document | `GET http://localhost:5242/openapi/v1.json` |
| Scalar UI | `http://localhost:5242/scalar` |

| Method | Route | Purpose |
|--------|-------|---------|
| GET | `/api/meters?location=&meterType=` | registered meters |
| GET | `/api/meters/{id}` | one meter |
| GET | `/api/meters/{id}/readings?metricCode=&from=&to=&page=&pageSize=` | that meter's readings, newest first |
| POST | `/api/readings` | submit a batch of readings |

`POST /api/readings` does not write to the database directly. It publishes `MeterReadingsCaptured`
to RabbitMQ, the same message the ingestor sends, and answers `202 Accepted` with the batch id. The
existing consumer then persists it, which means the submission gets the inbox deduplication, the
transactional outbox and the SignalR notification for free, with no second write path to keep in
sync.

```bash
curl -i -X POST http://localhost:5242/api/readings \
  -H "Content-Type: application/json" \
  -d '{"readings":[{"location":"Kitchen","meterType":"air_quality","metricCode":"co2","numeric":1450}]}'
```

That value is above the configured threshold, so a client connected to the notification service
receives both the reading and an alert within a second or two.

Each reading carries either `numeric` or `flag`, never both and never neither; a batch that breaks
that rule comes back as `400` with a per-item validation problem.

## GraphQL Gateway

Read-only GraphQL API over the same database, for the frontend.

| What | Where |
|------|-------|
| GraphQL endpoint | `POST http://localhost:5243/graphql` |
| Nitro IDE | `http://localhost:5243/graphql` in a browser |

Queries: `meters`, `readings` (cursor pagination), `filterOptions`, `latestReadings`,
`metricSnapshot`, `readingStats`, `readingSeries`, `locationStats`.

Filtering, sorting, bucketing and aggregation all happen in SQL. `metricSnapshot` and
`readingSeries` exist so the browser never has to group or rank anything: the first returns
one row per metric with its unit, its worst current location and whether it sits inside the
configured band, the second returns one bucketed series per location from a single
`GROUP BY`. `filterOptions` returns the distinct locations, meter types and metric codes, so
even the dropdowns are filled from the database rather than by de-duplicating a list in the
page.

### Exporting the schema

The frontend generates its types from `schema.graphql`, so the file is committed and has to be
refreshed whenever the schema changes:

```powershell
./Source/GraphQlGateway/export-schema.ps1
```

Or directly:

```bash
dotnet run --project Source/GraphQlGateway/GraphQlGateway.API --no-launch-profile -- schema export --output Source/GraphQlGateway/schema.graphql
```

## Dashboard

The frontend: React, TypeScript and Vite, served by nginx in its own container.

| What | Where |
|------|-------|
| Dashboard | `http://localhost:5180` |
| Dev server | `http://localhost:5173` after `yarn dev` |

```bash
cd Source/Frontend
yarn install
yarn dev
```

Yarn 4 through Corepack, which is bundled with Node: the exact version lives in `packageManager`
in `package.json`, so your machine, the Docker build and CI all resolve the same one. If `yarn`
is not on the path yet, `corepack enable` once is enough — do not install Yarn from npm, the
`yarn` package there only publishes 1.x. `.yarnrc.yml` turns off Plug'n'Play: Vite, TypeScript
and oxlint all expect a real `node_modules` tree.

The dev server and nginx both proxy `/graphql`, `/api` and `/hubs` to the gateway, the processor
and the notification service, so the page only ever talks to its own origin. That is why there is
no API URL to configure in the browser and no CORS to arrange: in the container the three targets
come from `GATEWAY_URL`, `PROCESSOR_URL` and `NOTIFICATIONS_URL`, in development from the proxy
table in `vite.config.ts`.

The panels are the dashboard tiles (`metricSnapshot`), the per-location chart (`readingSeries`),
the location table (`locationStats`), the paged reading list (`readings`, keyset cursors), the
live feed (SignalR) and the submit form (`POST /api/readings`). Nothing is filtered, sorted or
aggregated in the browser — every panel asks the server for exactly the rows it draws.

### Generated types

`src/gql/graphql.ts` is generated from the exported schema and committed, so neither the Docker
build nor CI needs a running gateway. Regenerate it whenever the schema changes:

```bash
./Source/GraphQlGateway/export-schema.ps1
cd Source/Frontend && yarn codegen
```

## Notification Service

Pushes readings to browsers over SignalR as soon as they are in the database.

| What | Where |
|------|-------|
| Hub | `ws://localhost:5244/hubs/readings` |
| Test client | `http://localhost:5244` in a browser |

The chain is `ingestor -> MeterReadingsCaptured -> processor -> MeterReadingsStored ->
notifications`. The processor writes the readings and the outgoing event in one transaction, so
a client is never told about a reading that is not yet queryable through GraphQL, and no reading
is stored without its notification.

### Transactional outbox

The processor uses the MassTransit Entity Framework outbox, so `MeterReadingsStored` is not sent
to the broker from inside the consumer. It is written to `OutboxMessage` in the same transaction
as the readings, and delivered once that transaction commits. If the process dies in between,
the redelivered `MeterReadingsCaptured` is recognised by `InboxState` and the pending outbox
messages are delivered instead of the consumer running twice.

That means three tables belong to MassTransit rather than to the domain — `InboxState`,
`OutboxState` and `OutboxMessage`. They are the reason `ProcessorDbContext` has an
`OnModelCreating` again: their mapping ships as fluent configuration and cannot be expressed
with annotations on entities we do not own.

`ProcessedMessages` still exists and still guards against duplicates. It overlaps with the inbox
but does not expire, while inbox rows are removed once the duplicate-detection window passes.

The outbox tables come from a migration, so after pulling this change:

```powershell
cd Source/DataProcessorService/DataProcessorService.API
dotnet ef migrations add Outbox --project ../DataProcessorService.DAL --startup-project .
```

### Subscribing

A connection belongs to exactly one group, so it never receives the same reading twice:

```js
await connection.invoke("Subscribe", null, null);            // readings:all
await connection.invoke("Subscribe", "Kitchen", null);       // readings:location:Kitchen
await connection.invoke("Subscribe", null, "co2");           // readings:metric:co2
await connection.invoke("Subscribe", "Kitchen", "co2");      // both
await connection.invoke("Unsubscribe");
```

The server calls two methods back: `ReadingsReceived(readings)` and, when a value leaves its
configured band, `AlertsRaised(alerts)`. An alert carries the reading it was raised for, the
threshold that was crossed and whether it went `Above` or `Below`.

### Thresholds

Configured per metric in `appsettings.json`; a metric without an entry never raises an alert,
and readings that carry a boolean instead of a number are skipped.

```json
"Thresholds": {
  "Metrics": [
    { "MetricCode": "co2", "Unit": "ppm", "Max": 1000 },
    { "MetricCode": "humidity", "Unit": "%", "Min": 30, "Max": 70 }
  ]
}
```

The same section is bound by the gateway, from `Shared.Configuration.ThresholdOptions`. The
notification service uses it to decide when to raise an alert; the gateway uses it so
`metricSnapshot` can return the band state and the unit with each metric, which keeps that
judgement on the server instead of in the page.

### Browser origins

The test client is served by the service itself, and the dashboard container reaches the hub
through its own nginx, so neither needs CORS. The Vite dev server does, because it opens the
WebSocket from `http://localhost:5173`: that origin is the `CLIENT_APP_ORIGIN` default and lands
in `ClientApp:AllowedOrigins`. SignalR sends credentials, so a wildcard origin is not an option.
