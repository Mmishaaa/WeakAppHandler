# ADR-0004: Real-time delivery (SignalR and GraphQL subscriptions) is single-replica only

## Status

Accepted — documented limitation, not a defect.

## Context

Two independent real-time channels exist: the Notification Service's SignalR hub (`AlertsHub`,
alerts raised/resolved) and the Gateway's GraphQL subscription (`onReadingStored`). Both are fed by
RabbitMQ consumers running inside the same process that serves the real-time connection —
`ReadingStoredSubscriptionConsumer` publishes into HotChocolate's own subscription pub/sub, and
`SignalRAlertDispatcher` calls `Clients.All.SendAsync` directly from the consumer that evaluated the
alert.

Both frameworks support scaling to multiple replicas through a shared backplane — SignalR via
`AddStackExchangeRedis` (or an equivalent), HotChocolate's subscriptions via a Redis or PostgreSQL
pub/sub provider in place of `AddInMemorySubscriptions()`. Neither is configured here.

## Decision

Ship with in-memory pub/sub on both channels, and treat "the service that owns a real-time channel
runs as exactly one replica" as a documented constraint rather than solve multi-replica fan-out now:

- `Program.cs` (Notification): `builder.Services.AddSignalR();` — no backplane extension.
- `Program.cs` (Gateway): `.AddInMemorySubscriptions()` — HotChocolate's own in-process provider.

A client connected to replica A of a horizontally-scaled Notification Service or Gateway would
never see an event that a RabbitMQ consumer running on replica B happened to process, because
nothing propagates that event across the process boundary. This is invisible at the current scale
(both services are meant to run as one instance each per the PRD's own deployment model, F11) but
would silently under-deliver the moment either service is scaled beyond one replica.

## Consequences

- Both real-time surfaces are correct and complete for exactly the deployment topology this system
  ships with (`docker-compose.yml`, one instance of each service). Scaling either service
  horizontally is not supported today and must not be attempted without first adding a backplane.
- The choice is symmetric across both channels for the same underlying reason, which is why the
  code comments in `Program.cs` (Gateway) and `Subscription.cs` cross-reference each other as
  sharing this ADR rather than documenting the limitation twice with two different rationales.
- If multi-replica real-time delivery becomes a requirement, the fix is additive — swap
  `AddInMemorySubscriptions()`/`AddSignalR()` for their backplane-backed equivalents — and does not
  require restructuring either consumer, since both already publish through their framework's own
  pub/sub abstraction rather than holding connections in a hand-rolled in-memory collection.
- This is explicitly out of scope for TASK-047 (compose deployment): bringing the stack up under
  compose does not itself require more than one replica of any service, so the limitation this ADR
  documents is not blocking that task.
