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

## GraphQL Gateway

Read-only GraphQL API over the same database, for the frontend.

| What | Where |
|------|-------|
| GraphQL endpoint | `POST http://localhost:5243/graphql` |
| Nitro IDE | `http://localhost:5243/graphql` in a browser |

Queries: `meters`, `readings` (cursor pagination), `latestReadings`, `readingStats`,
`locationStats`.

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
"Notifications": {
  "Thresholds": [
    { "MetricCode": "co2", "Max": 1000 },
    { "MetricCode": "humidity", "Min": 30, "Max": 70 }
  ]
}
```

### Browser origins

The test client is served by the service itself, so it needs no CORS. A frontend on another
origin does: list it in `ClientApp:AllowedOrigins`, or set `CLIENT_APP_ORIGIN` in `.env` for the
container. SignalR sends credentials, so a wildcard origin is not an option.
