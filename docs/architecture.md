# Architecture

## Runtime

![Runtime data flow: the ingestor polls WeakApp and publishes readings to RabbitMQ; the processor stores them in PostgreSQL with an outbox event; the notification service pushes that event to browsers over SignalR; the GraphQL gateway serves stored readings read-only through the dashboard's nginx.](architecture/runtime.svg)

Solid arrows are synchronous calls, dashed ones are messages through the broker. Every service
runs in docker-compose on one `backend` network.

The only way into the database is the processor. The ingestor and `POST /api/readings` both
publish the same `MeterReadingsCaptured` message, so a submitted reading takes exactly the path
of a polled one. The gateway holds a `SELECT`-only role, and the browser talks to nothing but
the dashboard's nginx.

### One reading, step by step

1. **Poll.** Every 10 s the ingestor calls `GET /meters` with `X-Api-Key`. Polly retries a
   5xx, a network fault or a timeout twice with backoff; a `429` is not retried, because the
   next tick is the backoff.
2. **Parse and publish.** Payloads that fail to parse are dropped, and an empty batch is not
   sent. The rest go out as one `MeterReadingsCaptured`. If the broker does not confirm within
   5 s the batch is dropped and counted in `weakapphandler.batches.publish_failed`.
3. **Consume once.** The processor's consumer checks `InboxState`, so a redelivered message is
   acknowledged without running again. Failures are retried three times.
4. **Store with its event.** Meters, readings and the outgoing `MeterReadingsStored` are written
   in one transaction. The outbox delivers the event only after the commit, so nobody is told
   about a reading GraphQL cannot return yet.
5. **Evaluate and push.** The notification service checks each value against `thresholds.json`
   and sends `ReadingsReceived` and, for breaches, `AlertsRaised` to the SignalR groups that
   match the reading: all, by location, by metric, or both.
6. **Read.** The dashboard asks the gateway for tiles, series, breakdowns and paged readings.
   Filtering, bucketing and aggregation run in SQL; the browser draws exactly the rows it
   receives.

## Delivery

![Delivery: a push runs the service pipelines, which publish images to GHCR under a dated branch tag, the commit sha and latest on main; deploy.yml takes a tag per service, and deploy.ps1 pulls those images, starts the stack, smoke-tests it and rolls back on failure.](architecture/delivery.svg)

A push rebuilds only the services whose files changed, so a release is one tag per service
rather than one tag for the stack. On a GitHub runner the stack is torn down after the smoke
tests; on a real server it would stay up and `.deploy-state.json` would make the rollback
useful. See [Deployment](../README.md#deployment) for the commands.

## Where the guarantees come from

| Guarantee | Mechanism |
|-----------|-----------|
| No duplicate writes | `InboxState` remembers each handled `MessageId` for a day, so a message RabbitMQ redelivers is acknowledged without touching the database again. |
| No notification without data | The outbox row commits with the readings. If the process dies in between, the redelivered message sends the pending event instead of storing twice. |
| Losses are counted, not hidden | A batch the broker refuses is dropped, logged as event `10` with its `BatchId`, counted in Prometheus and raised by the `IngestorBatchesDropped` rule. |
| Least privilege on reads | The gateway connects as `gateway`, which `db/init/01-gateway-role.sh` grants `SELECT` on every table the processor creates. |

The diagrams are hand-written SVG; edit them as text and keep every colour inside the `<style>`
block at the top of each file, which carries both the light and the dark palette.
