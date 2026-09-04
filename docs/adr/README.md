# Architecture Decision Records

Each ADR captures one decision that shapes how services are structured or allowed to talk to each
other — see [`docs/architecture.md`](../architecture.md) for how these fit into the system as a
whole.

| ADR | Decision |
|-----|----------|
| [0001](0001-ingestor-processor-responsibility-split.md) | The Ingestor has no database access; attempt outcomes travel as a message |
| [0002](0002-independent-migration-ownership.md) | Three independent migrating `DbContext`s; no cross-owner foreign keys |
| [0003](0003-no-gateway-domain-layer.md) | The Gateway has no Domain project |
| [0004](0004-signalr-single-replica-limitation.md) | Real-time delivery (SignalR and GraphQL subscriptions) is single-replica only |
