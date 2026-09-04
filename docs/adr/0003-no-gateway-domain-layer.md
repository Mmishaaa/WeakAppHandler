# ADR-0003: The Gateway has no Domain project

## Status

Accepted.

## Context

Every other backend service in this solution follows, or partially follows, Clean Architecture with
a `Domain` layer holding entities and business rules independent of any framework
(`WeakAppHandler.Processor.Domain` is the fullest example — entities, no EF Core, no ASP.NET Core
reference at all). The Gateway's own solution folder holds only `WeakAppHandler.Gateway.Api`,
`WeakAppHandler.Gateway.Application`, and `WeakAppHandler.Gateway.Infrastructure` — no
`WeakAppHandler.Gateway.Domain`.

This was a deliberate omission, not an oversight, and not a shortcut taken under time pressure: a
`Domain` layer exists to hold business rules and invariants a service is responsible for enforcing.
The Gateway enforces none. It is a read-composition and thin-proxy layer over data and rules three
other services already own and validate.

## Decision

The Gateway has two layers, not three:

- **`WeakAppHandler.Gateway.Application`** holds read models (`MeterReadModel`, `ReadingReadModel`,
  `AggregationBucketReadModel`, …) and the `IGatewayReadContext`/`IGatewayAlertingReadContext`
  interfaces those models are queried through. These are shapes for presenting data, not domain
  entities with behaviour — every property is a plain, mutable-by-construction record, and nothing
  in this layer enforces an invariant (that is the Processor's `AlertRule`'s job for alert rules, the
  Processor's `Reading`'s job for readings, and so on).
- **`WeakAppHandler.Gateway.Infrastructure`** implements those interfaces against two read-only EF
  Core contexts, and separately holds the Gateway's own entity mirrors
  (`ReadingEntity`, `MeterEntity`, `AlertEntity`, …) used only to shape the read-only queries — these
  are query-mapping types, not domain entities, and carry no methods.
- **`WeakAppHandler.Gateway.Api`** is everything a human or another service actually talks to:
  the GraphQL schema (`Query`, `Subscription`, `MeterResolvers`), the two admin/export REST
  controllers, and the `ReadingStoredSubscriptionConsumer` that feeds the subscription stream.

Every write the Gateway's own API surface accepts is one of:

1. **Proxied verbatim.** `AdminProxyController` forwards the Ingestor's/Processor's admin requests
   without inspecting or re-validating the body — the downstream service already validates it
   (see `ProcessingAdminController`'s and `IngestionAdminController`'s own validation), and
   re-validating in the Gateway would risk the two copies drifting apart.
2. **Not accepted at all.** Alert-rule CRUD, the one genuinely stateful write surface in this whole
   system's frontend, bypasses the Gateway entirely — the frontend calls the Notification Service's
   own REST API directly (`shared/config/runtimeConfig.ts`'s `notificationApiUrl`), because that is
   where the validation (`AlertRuleRequestValidator`) and the schema (`alert_rules`' check
   constraints) actually live.

## Consequences

- A future write the Gateway itself needed to validate and own would be the actual trigger to add a
  `WeakAppHandler.Gateway.Domain` project — this ADR documents why one does not exist today, not a
  permanent constraint against ever adding one.
- GraphQL mutations do not exist in this schema (`Query`/`Subscription` only) — there was never a
  write path to design one for. A reader looking for "where does the Gateway validate a mutation"
  will not find one, by design.
- The read models in `Gateway.Application` are free to be reshaped per-query (projection, filtering,
  paging via HotChocolate's middleware) without touching any other service's own domain model,
  because they were never meant to be that model — they are a read-side view, disposable and
  re-derivable from the owning service's real schema at any time.
