# Operations Guide

## Recurring Background Jobs

All recurring jobs are registered in the Worker process via `OsuSynchronizationRecurringJobRegistrar` and run by Hangfire.

| Job ID | Schedule | Description |
|--------|----------|-------------|
| `osu-sync-tier1` | Every 1 minute | Sync Tier 1 tracked players from osu! API |
| `osu-sync-tier2` | Every 5 minutes | Sync Tier 2 tracked players from osu! API |
| `osu-sync-tier3` | Every 15 minutes | Sync Tier 3 tracked players from osu! API |
| `inactivity-decay` | Daily at 03:00 UTC | Evaluate inactive players and apply price decay |

### Inactivity Decay Job

The `inactivity-decay` job runs daily and:

1. Queries all active tracked players.
2. Fetches the latest snapshot for each player.
3. Compares the snapshot `CapturedAt` timestamp against the configured threshold.
4. For players whose latest snapshot is older than the threshold, publishes a `PlayerInactive` event.
5. The existing `PlayerInactiveEventHandler` processes the event and applies decay through the market engine.

Configuration:

- `MarketEngine:InactivityThresholdDays` — number of days without a snapshot before a player is considered inactive. Default: `7`.
- `MarketEngine:InactivityDecayImpact` — the decay coefficient applied per evaluation. Default: `0.005` (0.5%).

The job is protected with `DisableConcurrentExecution` (60s timeout) and `AutomaticRetry` (3 attempts).

## Anti-Abuse Protections

Trading endpoints enforce the following protections via `TradingGuardService`:

### Trade Cooldown

Prevents a user from trading the same stock within the cooldown window.

- `AntiAbuse:TradeCooldownSeconds` — seconds between trades on the same stock per user. Default: `30`.
- Blocked trades return HTTP 400 with `TRADE_COOLDOWN` error code.

### Position Limit

Prevents a user from owning more than a percentage of a stock's total supply.

- `AntiAbuse:MaxOwnershipPercentage` — maximum ownership percentage per user per stock. Default: `25`.
- Not enforced when total supply is zero (allows first buyer).
- Blocked trades return HTTP 400 with `POSITION_LIMIT_EXCEEDED` error code.

### Rapid Trading Detection

Logs a structured warning when a user exceeds the trade count threshold within a time window.

- `AntiAbuse:RapidTradeWindowSeconds` — detection window in seconds. Default: `300`.
- `AntiAbuse:RapidTradeThreshold` — trade count that triggers a warning. Default: `10`.
- Non-blocking: trades still succeed, but a warning is logged for review.

All violations produce structured log entries with `UserId`, `StockId`, timestamps, and threshold values.

## Hangfire Dashboard

- URL: `/hangfire`
- Requires: authenticated user with `Admin` role
- Production: also requires HTTPS

Use the dashboard to monitor job execution, view failures, and manually trigger jobs if needed.

## Health Checks

Two endpoints verify application and dependency health:

- `/health`
- `/api/v1/health`

Both return a JSON response with individual check results:

```json
{
  "status": "Healthy",
  "checks": [
    { "name": "postgresql", "status": "Healthy", "duration": 12.3 },
    { "name": "redis", "status": "Healthy", "duration": 5.1 }
  ],
  "totalDuration": 15.8
}
```

| Status | HTTP Code | Meaning |
|--------|-----------|---------|
| Healthy | 200 | All dependencies reachable |
| Unhealthy | 503 | One or more dependencies unreachable |

Checked dependencies:

| Name | Tags | Description |
|------|------|-------------|
| `postgresql` | db, ready | PostgreSQL connectivity via `SELECT 1` |
| `redis` | cache, ready | Redis connectivity via `PING` |

Use these endpoints for Docker Compose health checks, load balancer probes, and monitoring systems.

## Key Configuration Sections

| Section | Description |
|---------|-------------|
| `AntiAbuse:*` | Trade cooldown, position limits, rapid trading detection |
| `MarketEngine:*` | Pricing coefficients, decay settings, price floor |
| `Cors:AllowedOrigins` | Allowed frontend origins for CORS |
| `Security:OAuthReturnUrl:AllowedOrigins` | Allowed OAuth return URL origins |
| `ConnectionStrings:Postgres` | PostgreSQL connection string |
| `ConnectionStrings:Redis` | Redis connection string |

See `docs/DEPLOYMENT.md` for full environment variable reference.
