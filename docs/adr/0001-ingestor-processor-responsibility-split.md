# ADR-0001: Ingestor has no database access; attempt outcomes travel as a message

## Status

Accepted.

## Context

The Ingestor polls WeakApp on a schedule and must record every attempt's outcome — success with a
reading count, or one of several failure modes (`http_error`, `rate_limited`, `unauthorized`,
`corrupted`) — into `ingest_batches`, the table PRD §7.1 defines for exactly this purpose. The
obvious approach would give the Ingestor its own connection string and let it write
`ingest_batches` directly.

That approach was rejected. The Ingestor's job is to poll a flaky external API on a tight interval
(PRD F1) — every additional dependency in its critical path is a way for polling itself to stall.
A database write, with its own connection-pool exhaustion, transient-failure, and retry semantics,
is exactly that kind of dependency, and it duplicates work the Processor already does for the
`readings.ingested` path.

## Decision

The Ingestor has **no database connection at all** — not even a read-only one. Every poll attempt,
regardless of outcome, is published as an `IngestAttemptRecorded` message
(`src/Contracts/WeakAppHandler.Contracts/IngestAttemptRecorded.cs`):

```csharp
public sealed record IngestAttemptRecorded(
    Guid MessageId,
    Guid BatchId,
    DateTimeOffset FetchedAt,
    IngestOutcome Outcome,
    int? HttpStatus,
    int DurationMs,
    int ReadingCount,
    string? ErrorMessage);
```

The Processor's `IngestAttemptRecordedConsumer` is the only thing that ever writes to
`ingest_batches`. A successful attempt *additionally* publishes `ReadingsIngested` (the actual
meter payloads) sharing the same `BatchId`, so the Processor can tie the readings it stores back to
the attempt row for that same poll — but a batch row is written for every attempt, successful or
not, from the one message every attempt produces regardless of outcome.

The two publishes are independent, non-transactional operations from the Ingestor's side: nothing
enforces that both `ReadingsIngested` and `IngestAttemptRecorded` land atomically, because nothing
needs to — the Processor's own idempotency (message-id deduplication via `processed_messages`)
already tolerates either message being redelivered or, in a partial-failure window, one arriving
without the other yet.

## Consequences

- The Ingestor's own resilience pipeline (retry/timeout/circuit-breaker) has one job and one
  external dependency: WeakApp. A RabbitMQ outage degrades publishing, not polling correctness.
- `ingest_batches` has a single writer (`WeakAppHandler.Processor.Infrastructure`), which is what
  makes the "three independent migrating `DbContext`s, no cross-owner FK" rule in
  [ADR-0002](0002-independent-migration-ownership.md) possible for this table in the first place.
- The Ingestor's admin REST surface (`/api/v1/ingestion/status`) reports on its own in-memory
  polling state (`IngestionRuntimeState`) rather than querying `ingest_batches` — it has nothing to
  query. A caller wanting the persisted history uses the Processor's own admin endpoint or the
  Gateway's `readings` query instead.
- A poll attempt that fails to publish at all (RabbitMQ unreachable) leaves no trace in
  `ingest_batches` — there is no local fallback store. This is accepted as consistent with the
  broader "communication only through the queue" rule (PRD §4.3): the alternative would need the
  Ingestor to hold a database connection specifically for a broker-outage edge case, undermining the
  whole point of this decision.
