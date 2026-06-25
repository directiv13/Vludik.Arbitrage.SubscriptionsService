# Subscriptions Service — CLAUDE.md

## Overview

The Subscriptions service is part of an Arbitrage Platform. Its responsibility is to deliver real-time spread data from Redis Pub/Sub to connected clients based on their active subscriptions, over WebSockets.

---

## Architecture

```
React.js Web App
      │ WebSocket
      ▼
Subscription Service
      │                        │
      ▼                        ▼
Redis (session state)    SQL DB (audit log)
      │
      ▼
Redis Pub/Sub ◄─── Market Data Service ◄─── Exchange N / Exchange 1
      │
      └──► Spread Job 1 / 2 / N
```

The Market Data service publishes tick data to Redis Pub/Sub. Both the Spread Jobs and the Subscription service consume from the same topics — this is the intended fan-out mechanism.

> **Note:** The service is a single instance (MVP). Horizontal scaling and SPOF mitigation are deferred.

---

## WebSocket Protocol

### Subscribe

```json
{
  "action": "subscribe",
  "params": {
    "symbol": "BTCUSDT",
    "buyExchange": {
      "name": "Binance",
      "contractType": "spot"
    },
    "sellExchange": {
      "name": "Aster",
      "contractType": "perpetual"
    }
  }
}
```

On receipt:
1. Store subscription in Redis with 35 s TTL (see [Session State](#session-state)).
2. Write a record to SQL `subscriptions` table with `status = 'active'`.
3. Publish `SubscriptionCreatedEvent` to RabbitMQ.
4. Begin consuming the relevant Redis Pub/Sub topics and forwarding messages to the client.

### Unsubscribe

```json
{
  "action": "unsubscribe",
  "params": {
    "symbol": "BTCUSDT",
    "buyExchange": {
      "name": "Binance",
      "contractType": "spot"
    },
    "sellExchange": {
      "name": "Aster",
      "contractType": "perpetual"
    }
  }
}
```

On receipt:
1. Delete the Redis subscription key.
2. Update SQL record to `status = 'deleted'`, set `closed_at`.
3. Publish `SubscriptionDeletedEvent` to RabbitMQ.
4. Stop forwarding messages for that topic to the client.

### Ping / Pong

The client must send a `ping` message every **30 seconds**.

```json
{ "action": "ping" }
```

On receipt: refresh the Redis TTL to 35 s. If no ping arrives within the TTL window, Redis expires the key automatically — the service detects this via keyspace notifications and treats it as an implicit unsubscribe (publishes `SubscriptionDeletedEvent`, updates SQL record).

---

## Redis Pub/Sub

### Topic key format

```
tick:{exchange}:{symbol}:{contractType}
```

Example: `tick:Binance:BTCUSDT:spot`

### Message schema

```json
{
  "exchange": "Binance",
  "symbol": "BTCUSDT",
  "contractType": "spot",
  "bestBid": 69000.045,
  "bestAsk": 69001.002,
  "receivedAt": "06/19/2026 23:13:06"
}
```

> **Stale data policy:** Redis Pub/Sub provides no message persistence. If the service is disconnected, messages published during that window are discarded. This is intentional — outdated data must not be sent to clients.

For each subscription, the service listens to **two topics** simultaneously (buy exchange + sell exchange) and forwards both streams to the client.

---

## Storage

### Redis — Active Session State

Primary store for runtime subscription state.

```
Key:   sub:{connectionId}
Value: {
  "symbol": "BTCUSDT",
  "buyExchange":  { "name": "Binance", "contractType": "spot" },
  "sellExchange": { "name": "Aster",   "contractType": "perpetual" },
  "subscribedAt": "<ISO datetime>",
  "lastPingAt":   "<ISO datetime>"
}
TTL: 35 seconds (reset on every ping)
```

Reverse index (topic → active connections):

```
Key:   topic-subs:tick:{exchange}:{symbol}:{contractType}
Value: Set { "conn-abc", "conn-xyz", ... }
```

TTL expiry cleanup is handled via **Redis keyspace notifications** — no cron jobs required.

### SQL DB — Audit / History

Append-only log of all subscription lifecycle events. Uses the existing `Jobs` SQL database.

```sql
CREATE TABLE subscriptions (
  id            UUID         PRIMARY KEY,
  connection_id VARCHAR(128) NOT NULL,
  symbol        VARCHAR(20)  NOT NULL,
  buy_exchange  VARCHAR(50)  NOT NULL,
  buy_contract  VARCHAR(20)  NOT NULL,
  sell_exchange VARCHAR(50)  NOT NULL,
  sell_contract VARCHAR(20)  NOT NULL,
  status        VARCHAR(20)  NOT NULL, -- 'active' | 'deleted' | 'expired'
  created_at    TIMESTAMPTZ  NOT NULL,
  closed_at     TIMESTAMPTZ
);
```

---

## RabbitMQ Events

| Event                    | Trigger                                              | Routing key             |
|--------------------------|-------------------------------------------------------|-------------------------|
| `SubscriptionCreatedEvent` | Client sends `subscribe` message                   | `subscription.created`  |
| `SubscriptionDeletedEvent` | Client sends `unsubscribe`, or ping TTL expires    | `subscription.deleted`  |

Both are published to the shared **topic exchange** `arbitrage.events` (consumed by the Market Data service, which binds one queue per event type by routing key).

### Payload schema

```json
{
  "subscriptionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "connectionId": "conn-abc",
  "symbol": "BTCUSDT",
  "buyExchange":  { "name": "Binance", "type": "spot" },
  "sellExchange": { "name": "Aster",   "type": "perpetual" },
  "reason": "expired",
  "timestamp": 1750000000000
}
```

- `reason` (`"unsubscribed"` | `"expired"`) is present only on `SubscriptionDeletedEvent`.
- `timestamp` is Unix epoch **milliseconds**, not the SQL audit row's `TIMESTAMPTZ`.
- `connectionId` and `reason` are extra fields the Market Data consumer's own event contract doesn't declare — harmless on the wire (unknown JSON properties are ignored on deserialize) and kept here for this service's own diagnostics/audit trail.

---

## Implementation Notes (as built)

The service is implemented in **.NET 9** using Clean Architecture (`API → Infrastructure → Application → Domain`). A few behaviours are not obvious from the spec above and matter when operating or extending the service:

### Redis keyspace notifications
- The expiry watcher needs Redis to emit expired-key events. On startup `RedisKeyspaceListener` runs `CONFIG SET notify-keyspace-events Ex`, which requires **admin mode** — the `IConnectionMultiplexer` is built with `AllowAdmin = true`. If Redis forbids `CONFIG` (e.g. managed/cloud Redis), this call is logged and skipped; the operator **must** enable `notify-keyspace-events Ex` server-side or expiry cleanup will never fire. The bundled `docker-compose.yml` starts Redis with `--notify-keyspace-events Ex` already set.
- The listener subscribes to `__keyevent@0__:expired` — **database index 0**. All Redis keys must live on DB 0 (the default) for this to work.

### Expiry needs a shadow key (`sub-meta:{connectionId}`)
- Redis delivers the `expired` event **after** the value is already gone, so the primary `sub:{connectionId}` key cannot be read inside the expiry handler. The store therefore also writes a **non-expiring** `sub-meta:{connectionId}` copy on subscribe; `ISubscriptionStore.GetAsync` transparently falls back to it. Both keys are removed on unsubscribe/expiry. (`sub:*` glob does not match `sub-meta:*`, so the two never collide.)

### Single terminal event per subscription (expiry vs. disconnect race)
- When a key expires, the handler closes the WebSocket, which in turn wakes the connection's receive loop and triggers its disconnect-cleanup (another unsubscribe). To avoid a **duplicate** `SubscriptionDeletedEvent` and a status overwrite, the expiry handler deletes Redis state and finishes its SQL/event work **before** closing the socket, and `SubscriptionRepository.CloseAsync` only closes a row whose status is still `active` (idempotent). Net guarantee: exactly one terminal event per subscription, with `reason = "expired"` winning over a late `"unsubscribed"`.

### Audit row id recovery
- The unsubscribe/expiry flows only have a `connectionId`, so `ISubscriptionRepository.GetActiveIdAsync(connectionId)` recovers the audit-row `Guid`. The `IX_subscriptions_connection_id_status` index backs this lookup. The `SubscriptionCreatedEvent` and the matching `SubscriptionDeletedEvent` therefore carry the **same `SubscriptionId`**.

### RabbitMQ (MassTransit) — for event consumers
- Both events are published to a single shared **topic exchange** named `arbitrage.events` (not MassTransit's default per-message-type fanout exchange) — `DependencyInjection.AddInfrastructure` configures this per message type via `cfg.Message<T>(...).SetEntityName(...)`, `cfg.Publish<T>(...).ExchangeType = ExchangeType.Topic`, and a fixed routing key per type via `cfg.Send<T>(...).UseRoutingKeyFormatter(...)` (`subscription.created` / `subscription.deleted`). The Market Data service binds one queue per event type to this exchange by routing key — see [Payload schema](#payload-schema) for the wire shape it expects. The service itself registers **no consumers** (publish-only).
- Pinned to **MassTransit 8.x** intentionally — the 9.x line requires a commercial runtime license.

### Tick forwarding
- `TickSubscriber` keeps **one** Redis subscription per topic and fans out to N per-connection forwarders. Forwarded payloads preserve the wire schema, including the `receivedAt` format `MM/dd/yyyy HH:mm:ss`. Sends to a single socket are serialized (a `WebSocket` does not allow concurrent sends); failed sends are dropped (no buffering).

### Container build (`Dockerfile`) and the `api` compose service
- Multi-stage build (`sdk:9.0` → `aspnet:9.0`) rooted at the repo root so it can `COPY` all four `src/*` project files for restore caching before copying the rest of the source. Runs as the non-root `$APP_UID` user from the base image.
- `appsettings.json` hardcodes `"Urls": "http://localhost:8080"` for local `dotnet run` use. That key **cannot** be overridden with the conventional `ASPNETCORE_URLS` environment variable: ASP.NET Core's no-prefix `AddEnvironmentVariables()` call (used by the app's own configuration, as opposed to the early hosting-bootstrap phase) keeps the `ASPNETCORE_` prefix on the key, so it never collides with `Urls` from `appsettings.json` — the JSON value silently wins and Kestrel binds to loopback-only inside the container. The image therefore sets `ENV Urls=http://+:8080` (matching the literal config key) so it binds to all interfaces by default; this is also why `Urls`, not `ASPNETCORE_URLS`, is the correct env var to use if a caller wants to override the listen address.
- `docker-compose.yml` builds this image as the `api` service and connects it to `redis`/`rabbitmq`/`postgres` by service name via the standard `Section__Key` env var convention (e.g. `Redis__ConnectionString=redis:6379`, `ConnectionStrings__SubscriptionsDb=Host=postgres;...`) — same mechanism, and same `SubscriptionsDb` key, that the platform-level `Vludik.Arbitrage/docker-compose.yml` must use when it builds this service from `../Vludik.Arbitrage.SubscriptionService`.

---

## Build, Run & Local Verification

```bash
docker compose up -d --build                           # Redis (6379) + RabbitMQ (5672/15672) + Postgres (5432) + the API itself (8080)
dotnet dotnet-ef database update \                     # apply the EF migration (run once, from the host)
  --project src/SubscriptionsService.Infrastructure \
  --startup-project src/SubscriptionsService.Infrastructure
```

The `api` service builds from the root `Dockerfile` and is wired to the other containers by service name — see its `environment:` block in `docker-compose.yml`. For fast local iteration without rebuilding the image on every change, start only the infra and run the API on the host instead:

```bash
docker compose up -d redis rabbitmq postgres
dotnet run --project src/SubscriptionsService.API      # WebSocket endpoint: ws://localhost:8080/ws
```

- Local SQL database is `arbitrage` (see `appsettings.json`); the platform's shared `Jobs` DB is the intended production home for the audit table.
- `sim/client.mjs` is a dependency-free simulator (Node 22+) that acts as both the **React client** (WebSocket) and the **Market Data service** (publishes ticks to Redis over a raw TCP/RESP socket). Example — full lifecycle (subscribe → tick fan-out → ping → unsubscribe):
  ```bash
  node sim/client.mjs
  ```
  Expiry test (subscribe, then stay silent so the 35 s TTL lapses and the server closes the socket):
  ```bash
  node sim/client.mjs --symbol=SOLUSDT --holdMs=50000 \
    --scenario='[{"at":1000,"action":"publishBuy"},{"at":50000,"action":"close"}]'
  ```

---

## Known Limitations

- **Single instance, no horizontal scaling.** The service is built and deployed as one instance (MVP). In-memory connection/forwarder state (`TickSubscriber`, per-connection WebSocket forwarders) is not shared across instances, so running more than one instance would split topic fan-out and duplicate or drop client traffic unpredictably. There is also no sticky-session or shared-state mechanism, so a second instance is **not** a safe way to get more capacity or to mitigate the resulting single point of failure (SPOF) today — see Future Improvements below for the planned fix.

---

## Future Improvements

- **Authentication** — validate a JWT/Bearer token on the WebSocket handshake (`Upgrade` request) before accepting the connection.
- **Timestamp staleness check** — inspect `receivedAt` on each incoming Redis message; if it exceeds a configured threshold (e.g. 5 s), push a warning event to the client rather than the tick data.
- **Spread Job liveness validation** — before confirming a subscription, verify the relevant Spread Job is active and publishing to the expected Redis topic.
- **Horizontal scaling** — introduce sticky sessions (Nginx `ip_hash`) or a shared fan-out bus to support multiple Subscription service instances.
- **RabbitMQ reliability** — add an outbox pattern for `SubscriptionCreatedEvent` / `SubscriptionDeletedEvent` to handle broker unavailability.