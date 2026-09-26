# WeakAppHandler

## Running the stack

```bash
cp .env.example .env
docker compose up -d --build
```

That brings up everything: the third-party API, RabbitMQ, PostgreSQL, pgAdmin, the
observability stack (Prometheus, Loki, Tempo, Grafana) and all four services.
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
| Prometheus | http://localhost:9090 |
| Loki | http://localhost:3100 |
| Tempo | http://localhost:3200 |
| Grafana | http://localhost:3000 (admin/admin) |

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

## Logging

Serilog, wired once in `Shared/Logging` and switched on by a single line in each service's
`AddApi`. The setup clears the default providers first, so nothing is written twice, and opens
the Microsoft.Extensions.Logging filter all the way to `Trace` — otherwise it would cut messages
before Serilog ever sees them, and the levels would be configured in two places that disagree.
Levels live under `Serilog:MinimumLevel` in `appsettings.json`.

Every line carries the service it came from, and consumers push the identifiers of the work they
are doing onto the log context:

```csharp
using var batchScope = LogContext.PushProperty("BatchId", message.BatchId);
using var messageScope = LogContext.PushProperty("MessageId", messageId);
```

Everything logged while that scope is open inherits both, including framework messages the code
never touches. A batch can therefore be followed across three services by one identifier: the
ingestor publishes it, the processor stores it, the notification service broadcasts it.

The console template ends with `{Properties:j}`, which prints those attached values as JSON. The
structure is in the events themselves, so pointing the sink at a log store is a change of sink,
not of code — swapping the `outputTemplate` argument for `"formatter":
"Serilog.Formatting.Compact.CompactJsonFormatter, Serilog.Formatting.Compact"` emits newline-
delimited JSON instead.

`UseSerilogRequestLogging` is enabled on the processor and the gateway, where it collapses the
framework's several lines per request into one with the route, status code and duration. The
ingestor has no client traffic and the notification service holds long-lived connections, so
neither gains anything from it.

## Metrics

Every service exports OpenTelemetry metrics on `/metrics` in Prometheus format. `docker compose up`
brings up the two pieces that consume them:

| | Address | Credentials |
|---|---|---|
| Prometheus | <http://localhost:9090> | none |
| Grafana | <http://localhost:3000> | `admin` / `admin`, anonymous viewing is on |

Grafana arrives provisioned — the three datasources and the **WeakAppHandler overview**
dashboard are all read from `observability/` at startup, so there is nothing to import by hand.

Three layers of metrics are exported:

- **Auto-instrumented**: inbound requests (`http.server.request.duration`), outbound HTTP
  (`http.client.request.duration`), and the .NET runtime.
- **Library meters**: `MassTransit` for queue consume rates and faults, `Npgsql` for connection
  pool and command counts.
- **Domain counters**, defined in `Shared/Telemetry/IngestionMetrics.cs`:

| Metric | Tags | Recorded by |
|---|---|---|
| `weakapphandler.readings.ingested` | `meter_type` | the ingestor, once a batch is on the queue |
| `weakapphandler.readings.stored` | — | the processor, after the transaction commits |
| `weakapphandler.batches.duplicate` | — | the processor, when a redelivered message is skipped |
| `weakapphandler.alerts.raised` | `kind` | the notification service, per threshold breach |

The exporter renames these on the way out: dots become underscores and counters gain `_total`, so
`weakapphandler.readings.ingested` is queried as `weakapphandler_readings_ingested_total`.

The dashboard's most useful panel is **WeakApp responses** — outbound calls from the ingestor
broken down by status code. That is where the rate limiter becomes visible: a run that stays on
`200` is healthy, a climbing `429` line means the poll budget described above is being exceeded.

## Logs and traces

The same `observability/` folder brings up two more stores, and Grafana is provisioned against
both:

| | What lands there | Queried in Grafana as |
|---|---|---|
| Loki | every Serilog event from all four services | the **Loki** datasource |
| Tempo | OpenTelemetry spans: inbound requests, outbound HTTP, PostgreSQL commands, MassTransit publish and consume | the **Tempo** datasource |

**Logs.** The Loki sink is a second Serilog sink beside the console one, configured in each
`appsettings.json`; compose points it at `http://loki:3100`. Console output is kept, so
`docker compose logs` still works. `Application` becomes a Loki label, so
`{Application="DataIngestorService"}` narrows to one service; everything else Serilog attaches
— `BatchId`, `MessageId`, `TraceId` — travels as structured metadata and is filterable with
`| json`.

**Traces.** Tracing is switched on by `Telemetry:OtlpEndpoint`. When it is unset the exporter is
never registered, so running a service from the IDE without the stack up costs nothing; compose
sets it to `http://tempo:4317`. A trace follows one WeakApp poll from the ingestor's HTTP call,
through the RabbitMQ hop, into the processor's `INSERT`, and on to the notification fan-out,
because MassTransit propagates the trace context through the message headers.

**Correlation.** `ActivityEnricher` in `Shared/Logging` puts the current `TraceId` and `SpanId`
on every log event, so a log line and the trace it belongs to carry the same identifier. To jump
from one to the other, copy the `TraceId` out of the log and paste it into Tempo's **TraceQL**
search. The datasources deliberately declare no `uid` — Grafana 13 refuses to provision a
datasource that has one, and the failure takes the whole server down — which is also why the
usual click-through link between them is not wired up.

## Tests

One test project per service, so each maps onto its own pipeline.

```powershell
dotnet test Source\DataIngestorService\DataIngestorService.slnx
dotnet test Source\DataProcessorService\DataProcessorService.slnx
dotnet test Source\GraphQlGateway\GraphQlGateway.slnx
dotnet test Source\NotificationService\NotificationService.slnx

cd Source\Frontend; yarn test
```

xUnit with AwesomeAssertions and Moq; Vitest on the frontend. What is covered is deliberately the
behaviour that actually broke while the project was being built, so the suite is a record of the
bugs rather than a coverage number:

| Where | What it pins down |
|-------|-------------------|
| `MeterPayloadParserTests` | truncated, empty and wrongly typed payloads yield no readings instead of throwing |
| `WeakAppResiliencePipelineTests` | `429` is not retried even when `Retry-After` is present, `5xx` and transport faults are |
| `ReadingBatchServiceTests` | a redelivered batch writes nothing, a known meter is reused, every reading takes the batch's capture time |
| `ReadingStatsServiceTests` | the bucket size reaches the database instead of being rolled in memory, and threshold state is decided server-side |
| `NotificationDispatchServiceTests` | the fan-out covers all four group shapes, and an alert reaches every group that sees its reading |
| `windows.test.ts` | window bounds snap to bucket boundaries and hold steady between them, so an idle tab issues no queries |
| `format.test.ts` | numbers, units and timestamps render the same way whatever the reading carries |

The resilience pipeline is exercised through Polly itself rather than through a stubbed
`HttpClient`, so the test drives the same configuration the service runs.

### Integration tests

Two suites talk to a real PostgreSQL, started and thrown away per run by
[Testcontainers](https://dotnet.testcontainers.org/). They need a running Docker daemon and nothing
else — no connection string, no seeded database, no `docker compose up` beforehand. Without Docker
these tests fail to start; the unit tests in the same projects are unaffected.

| Where | What it pins down |
|-------|-------------------|
| `ReadingBatchServiceDatabaseTests` | the migrations apply cleanly, the transaction commits readings and meters together, and a redelivered message leaves the row count where it was |
| `ReadingRepositoryDatabaseTests` | every aggregate really is computed by PostgreSQL: hourly and daily bucketing, grouping by location and by meter type, `ORDER BY AVG(...) DESC`, the boolean `trueCount` / `trueShare` pair, and the latest-per-meter join |

The gateway builds its schema with `EnsureCreated`, because it owns no migrations — it reads the
tables the processor owns. The processor's fixture runs `Migrate`, so a migration that does not
apply fails the suite rather than production.

## Continuous integration

One workflow per service plus one for the frontend, under `.github/workflows/`. Each is scoped by
`paths`, so a change to the gateway does not rebuild the ingestor, and each runs the same four
stages: lint, build, test, Docker image.

| Workflow | Lint | Build | Test | Image |
|----------|------|-------|------|-------|
| `data-ingestor-service.yml` | `dotnet format --verify-no-changes` | `dotnet build -c Release` | `dotnet test` | root-context `docker build` |
| `data-processor-service.yml` | same | same | unit + Testcontainers | same |
| `graphql-gateway.yml` | same | same | unit + Testcontainers | same |
| `notification-service.yml` | same | same | `dotnet test` | same |
| `frontend.yml` | `yarn lint` (oxlint) | `yarn build` (`tsc -b` + Vite) | `yarn test` (Vitest) | `docker build Source/Frontend` |

`deploy.yml` sits beside them and is described under [Deployment](#deployment).

Analyzer violations do not need their own stage: `Directory.Build.props` sets
`TreatWarningsAsErrors`, so the build step fails on them. The frontend pipeline additionally reruns
`yarn codegen` and fails on a diff, which turns a schema the committed GraphQL types no longer match
into a red build instead of a runtime error.

Both package caches are keyed off the files that decide them — `Directory.Packages.props` for NuGet,
`yarn.lock` for Yarn — and `yarn install --immutable` refuses to resolve anything the lockfile does
not already pin.

## Deployment

There is no remote environment in this project, so `scripts/deploy.ps1` does against the local
Docker daemon exactly what it would do against one: pin a tag, roll the stack onto it, refuse to
call the deploy finished until every endpoint answers, and put the previous tag back when it does
not.

```powershell
./scripts/deploy.ps1                 # build the current commit, deploy it, smoke test it
./scripts/deploy.ps1 -Tag 1.4.0      # deploy a specific tag
./scripts/deploy.ps1 -SkipBuild      # redeploy images that already exist
./scripts/deploy.ps1 -DryRun         # print the commands without touching anything
./scripts/deploy.ps1 -Rollback       # go back to the tag that last passed
```

What a run does:

1. Creates `.env` from `.env.example` if it is missing.
2. Builds every image as `weakapphandler/<service>:<tag>`, the same names the CI pipelines
   produce. `IMAGE_TAG` is what `docker-compose.yml` substitutes, which is what makes a specific
   build addressable at all.
3. `docker compose up -d --wait`, so the script blocks until the containers with health checks
   report healthy and the rest are running.
4. Probes all ten services — WeakApp, the four .NET services, the dashboard, Prometheus, Loki,
   Tempo and Grafana — asking Docker which host port each one actually got rather than trusting
   `.env`.
5. Writes the tag to `.deploy-state.json`, keeping the one before it.

A failure at any of those steps re-deploys the tag recorded as current and then reports the
failure, so a broken build leaves the previous one running rather than a half-started stack.

`.github/workflows/deploy.yml` runs the same script on a tag push or on demand, with the runner
itself as the target host: it deploys, smoke-tests, dumps container logs if anything failed, and
tears the stack down. That is a real execution of the deploy path rather than a stub that echoes
the steps.

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
`metricSnapshot`, `readingStats`, `readingSeries`, `locationStats`, `meterTypeStats`.

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
the breakdown table, the paged reading list (`readings`, keyset cursors), the live feed (SignalR)
and the submit form (`POST /api/readings`). Nothing is filtered, sorted or aggregated in the
browser — every panel asks the server for exactly the rows it draws.

The breakdown table switches between two dimensions, and the switch changes which query runs
rather than how the rows are processed: `locationStats` groups by location and metric,
`meterTypeStats` by meter type and metric.

The two behave differently on purpose. By location follows the metric picked in the toolbar and
orders by the average descending, so the first row is the peak and the bar widths are a ratio
against it. By type ignores that filter and lists every metric, because each metric here comes
from exactly one kind of meter — filtered to one metric, the grouping would always collapse to a
single row. Rows from different metrics are not on one scale, so that view is ordered by type and
metric and drops the comparison bar.

Boolean metrics such as `motion_detected` have no number to average, so every aggregate carries
two more columns from the same `GROUP BY`: `trueCount`, the number of readings that were true,
and `trueShare`, the fraction of the bucket they make up.

The share is what gets drawn, not the count, and that distinction matters. A count rises and
falls with how many readings happened to land in the bucket, so the newest bucket always dips
because it is still filling and any gap in ingestion shows up as a trough — the picture ends up
describing the ingestor's uptime rather than the sensors. A share is immune to that: half the
readings detecting motion reads as 50% whether the hour holds twelve samples or three hundred
and sixty.

Boolean metrics are also drawn differently. Six smoothed lines suit a continuous quantity, not a
sensor that is either firing or not, and at 0 and 1 the locations would sit on top of each other.
So the panel switches to a state strip: one row per location, one cell per bucket, shaded by the
share, with the exact counts on hover. Nothing overlaps, and a bucket with no readings at all is
hatched rather than drawn as zero.

A metric whose rows all come back with a null average is drawn that way automatically. The table
keeps min, max and average — blank for boolean rows — and adds detection and share columns
whenever any row has detections, which is what makes the mixed by-type view readable.

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
