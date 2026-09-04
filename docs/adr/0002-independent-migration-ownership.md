# ADR-0002: Three independent migrating DbContexts; no cross-owner foreign keys

## Status

Accepted.

## Context

Four services persist data into one physical PostgreSQL instance (`weakapphandler`): Auth, the
Processor, and the Notification Service each own and migrate their own tables; the Gateway reads
across two of those services' schemas but owns none of its own. Left unconstrained, EF Core makes
it easy to model a foreign key from one service's table into another's — `alert_rules.meter_id`
referencing the Processor's `meters` table would compile, migrate, and query without complaint.

That was rejected as a structural hazard specific to this system's deployment model. Each service
is migrated and deployed independently; a cross-owner foreign key would mean the Notification
Service's migrations could fail or drift depending on the Processor's own schema state, and a
`meters` row the Processor deletes (it does not, today, but the constraint would then exist for no
reason) could not be removed without Notification's cooperation. The two services would no longer
be independently deployable, which contradicts the whole reason they are separate services.

## Decision

Each service that owns rows migrates them through exactly one `DbContext`, and no `DbContext`
anywhere in the solution declares a foreign key into a table a different context owns:

| `DbContext` | Project | Owns |
|---|---|---|
| `AuthDbContext` | `WeakAppHandler.Auth` | `users`, `service_clients`, `refresh_tokens`, `signing_keys` |
| `CoreDbContext` | `WeakAppHandler.Processor.Infrastructure` | `meters`, `readings`, `meter_current_state`, `ingest_batches`, `processed_messages` |
| `AlertingDbContext` | `WeakAppHandler.Notification.Api` | `alert_rules`, `alerts`, `alert_rule_state` |

A cross-service reference that in a single-database, single-context design would be a foreign key
is instead one of:

1. **A value copied at write time.** `alerts.rule_id` and `alert_rule_state` key off values the
   Notification Service already owns; nothing here needed a Processor reference in the first place.
   Where a value genuinely originates elsewhere — `meter_id`, `location`, `meter_type` on an alert —
   it is copied from the triggering `ReadingStored` event at write time rather than joined at read
   time, so it survives even if the Processor's own row for that meter later changes.
2. **A message field, not a database join.** The Processor's previous value for change detection
   (ADR discussion in `docs/architecture.md`'s communication rules) arrives inside `ReadingStored`
   rather than being looked up.
3. **A read-only database role, for the Gateway specifically.** `GatewayReadDbContext` and
   `GatewayAlertingReadDbContext` both point at the same physical database as the Processor's and
   Notification's own contexts, through a role granted `SELECT` only — a real SQL join
   (`Readings.Join(Meters, ...)` in `GatewayReadContext.cs`) is fine here because the Gateway never
   writes and never migrates; it is a query composition layer over tables it does not own, not a
   fourth owner.

## Consequences

- Three independent EF Core migration histories, each safe to apply, roll back, or fail
  independently of the other two. A broken Notification migration cannot block Processor or Auth
  deployments and vice versa.
- The Gateway's two read contexts (`GatewayReadDbContext`/`GatewayAlertingReadDbContext`) run
  `ApplyConfiguration` per entity, explicitly, rather than `ApplyConfigurationsFromAssembly` — a
  real bug (TASK-032) surfaced when both contexts' entity configurations lived in the same assembly
  and `ApplyConfigurationsFromAssembly` silently let each context absorb the other's entities. The
  read-only role does not by itself prevent structural leakage between contexts that share an
  assembly; explicit per-entity configuration is what actually enforces the boundary.
- No service can query another's tables through a compile-time-checked navigation property — a
  join like `GatewayReadContext.Readings`'s meter/location denormalisation has to be written by
  hand (`FromSqlInterpolated` or an explicit `Join`) against the read-only context, which is a
  deliberate cost: it keeps the coupling visible in the code that does it, rather than hidden behind
  a navigation property that looks like a normal EF Core relationship.
