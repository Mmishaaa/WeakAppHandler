# WeakAppHandler

## Quickstart

```bash
git clone <this repository> weakapphandler
cd weakapphandler
cp .env.example .env
docker compose up -d --build
```

That's it - no other step. Give the stack a minute or two to build the six .NET images and reach a
healthy state (`docker compose ps` shows every service `healthy`), then open
**http://localhost:8086** and log in with one of the seed credentials below (`viewer@weakapphandler.local`
/ `Viewer#12345` for a read-only tour, `admin@weakapphandler.local` / `Admin#12345` for the
Administration screen too). The dashboard populates itself - the Ingestor starts polling WeakApp the
moment its own container is healthy, with no manual seeding step.

Other endpoints worth knowing about once the stack is up:

| What | URL |
|------|-----|
| Frontend dashboard | http://localhost:8086 |
| Gateway GraphQL | http://localhost:8084/graphql |
| Grafana (F10 metrics dashboard) | http://localhost:3000 (`admin`/`admin`, see `.env.example`) |
| RabbitMQ management UI | http://localhost:15672 (`admin`/`admin_rmq_password`) |

`docker compose restart` (or a full `down`/`up` without `-v`) preserves all data - Postgres and
RabbitMQ both use named volumes, so readings, alert rules and seed users survive a restart.

## Demo scenarios (PRD §11)

Everything below assumes the stack from Quickstart is already up. Automated ones live in
`frontend/tests/e2e/` (`npm run test:e2e --prefix frontend`, or `cd frontend && npx playwright test`)
and read the same host-published ports as Quickstart; override with the `E2E_*` environment
variables in `frontend/tests/e2e/helpers/env.ts` for a non-default setup.

- **S1 - Cold start.** *Automated* (`s1-cold-start.spec.ts`). Confirms the dashboard shows real,
  already-ingested meter data with no manual intervention beyond logging in.
- **S2 - Injected failure.** *Automated* (`s2-injected-failure.spec.ts`). Stops the `weakapp`
  container for ~15s, confirms the dashboard keeps showing its last-known data (not a blank
  screen), restarts it, and confirms a fresh login shows the system healthy again. To watch it by
  hand instead: `docker compose stop weakapp`, watch the Ingestor's admin stats or the dashboard
  staying populated, `docker compose start weakapp`.
- **S3 - Queue back-pressure.** *Automated* (`s3-queue-backpressure.spec.ts`), via RabbitMQ's
  management HTTP API. Stops `processor`, confirms `readings.ingested` accumulates messages, restarts
  it, confirms the queue drains back to empty. Manual equivalent, useful if the management API isn't
  reachable from your machine for some reason: `docker compose stop processor`, then
  `docker exec <rabbitmq container> rabbitmqctl list_queues name messages -p weakapphandler` a few
  times to watch `readings.ingested` grow, `docker compose start processor`, then watch it drain back
  to 0.
- **S4 - Duplicate delivery.** *Tool*: `dotnet run --project tools/ReplayIngestedMessage`. Publishes a
  synthetic reading, waits for the Processor to record it, republishes the identical message (same
  message id), and confirms in the database that the reading count for that batch is unchanged - the
  Processor deduplicated it. Prints `PASSED`/`FAILED` and exits non-zero on a real duplicate.
- **S5 - Live alert.** *Automated* (`s5-live-alert.spec.ts`) confirms the realtime channel an alert
  would travel over (SignalR + the GraphQL subscription) is actually connected, not just present in
  the DOM. Reliably provoking a specific threshold crossing on demand needs reaching into WeakApp's
  own data generator, which is out of this suite's scope - to see the full scenario, open
  **Administration → Alert rules**, lower a rule's threshold to something the live data will cross on
  its next poll, then watch the Alerts page: the alert appears without a page refresh and resolves
  once the value returns to normal.
- **S6 - Authorisation.** *Automated* (`s6-authorization.spec.ts`). Logs in as `viewer`, confirms the
  Administration nav item and route are both absent (not just visually hidden), and that a direct call
  to the Notification Service's admin API with the viewer's own token is refused (401/403) - the
  server enforces this, not just the UI.
- **S7 - Traceability.** *Manual* - there is no distributed tracing collector (Jaeger/Zipkin/OTLP) in
  this compose stack, so there is no single trace UI to follow a reading through. What you can follow
  instead: every HTTP request carries an `X-Correlation-Id` response header, correlating one
  request within one service's own logs; across the async pipeline, a poll's `batchId` (visible in
  the Ingestor/Processor logs and in `ingest_batches`) connects Ingestor → Processor, and from there a
  reading's `(meterId, metricCode, observedAt)` connects Processor's own write through to the
  `ReadingStored` event Notification and the Gateway's subscription bridge both receive - tail
  `docker compose logs -f ingestor processor notification gateway` while watching one meter to see
  the same reading cross every log.

## Auth Service — seed credentials

Applying the Auth Service's EF Core migrations (`InitialAuth`) seeds one user per role plus one
machine-to-machine service client, for local development only:

| Role | Email | Password |
|------|-------|----------|
| `viewer` | `viewer@weakapphandler.local` | `Viewer#12345` |
| `admin` | `admin@weakapphandler.local` | `Admin#12345` |

| Service client | Client ID | Client secret | Scope |
|-----------------|-----------|----------------|-------|
| Gateway → Ingestor | `gateway-ingestor` | `gateway-ingestor-secret-CHANGE-ME` | `ingestion:admin` |

These values are defined in `src/Auth/WeakAppHandler.Auth/Persistence/Configurations/AuthSeedData.cs`
and are baked into the `InitialAuth` migration as password/secret hashes (PBKDF2-SHA256), not
plaintext. They are development-only defaults, not meant to be used as-is in any shared or
production-like environment.

## Auth Service — user login, refresh and JWKS

`POST /login` accepts `{ "email": "...", "password": "..." }` for one of the seed users above. On
success it returns `{ accessToken, tokenType, expiresInSeconds, role, email }` and sets an
`httpOnly`, `Secure`, `SameSite=Strict` `refresh_token` cookie. `role` is PascalCase (`"Viewer"` /
`"Admin"`), matching the role values `WeakAppHandler.ServiceDefaults.Auth.ServicePolicies`' policies
check — not the lowercase value stored in `users.role`.

Access tokens are RS256-signed JWTs (`iss=weakapphandler-auth`, `aud=weakapphandler`), default
lifetime 15 minutes (`Auth:Tokens:AccessTokenLifetimeMinutes`). `GET /.well-known/jwks.json`
publishes the public half of the signing key any service can validate them against; the private key
itself is generated once and persisted in the `signing_keys` table so it - and the token `kid` - stay
stable across Auth Service restarts.

`POST /refresh` reads the `refresh_token` cookie, and if it is still valid and unused, issues a new
access token and rotates the refresh token (the old one is revoked, a new cookie is set). Refresh
tokens default to a 7-day lifetime (`Auth:Tokens:RefreshTokenLifetimeDays`) and are stored only as a
SHA-256 hash in the `refresh_tokens` table, never in plaintext.

## Auth Service — client-credentials grant

`POST /token` accepts `{ "clientId": "...", "clientSecret": "..." }` for a seed service client (see
above). On success it returns `{ accessToken, tokenType, expiresInSeconds, scope }`. This is the
only synchronous inter-service path in the system (Gateway → Ingestor); the returned access token
is an RS256 JWT with the same `iss`/`aud`/lifetime as a user access token and validates through the
same `GET /.well-known/jwks.json` key, but carries no `role` claim — instead `sub` is the client id
and `scope` is a space-separated list of the client's granted scopes (e.g. `ingestion:admin`). An
unknown client id or wrong secret returns `401 Unauthorized`.