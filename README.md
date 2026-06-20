# OsuStocks.API

Backend service for the osu! Stocks game platform.

This repository uses a modular monolith with Clean Architecture and vertical slices. Current implementation includes authentication, player registry, synchronization, trading, portfolio, market engine, market intelligence (activity feed, OHLC candles, stock analytics, trending, leaderboards), notifications, and admin management.

## Current Status

Implemented modules:

- Solution skeleton: `Api`, `Application`, `Domain`, `Infrastructure`, `Worker`, `Shared`
- Persistence: EF Core + PostgreSQL mappings and migrations
- Infrastructure wiring: Redis cache, Hangfire, MediatR, FluentValidation, Mapster, Swagger
- Authentication: osu OAuth login/callback + JWT issuance + `/auth/me` (returns `avatarUrl` + `countryCode`)
- Player Registry: add/list/search/enable/disable tracked players (records `avatarUrl` + `countryCode`)
- Synchronization: tracked-player snapshot sync + market event persistence (tiered: 1m/5m/15m)
- Trading: buy/sell/history with maintenance mode guard
- Portfolio: holdings and portfolio summary
- Wallet: balance and transaction ledger
- Market Engine:
  - Inputs: `BuyOrderExecuted`, `SellOrderExecuted`, `TopPlayDetected` (scaled by the play's pp relative to the player's pp), `PpIncreased` (symmetric — gains lift, losses lower), `RankChanged` (bidirectional — climbing lifts price, slipping lowers it), `PlayerInactive`
  - Output: `PriceChanged`
  - Multiplicative, per-event-capped, price-floored pricing + stock price history recording
- Market Intelligence (Phase 2):
  - Market activity event feed: global (`/market/events`) and per-stock (`/market/events/{stockId}`)
  - OHLC candles via `GET /market/stocks/{id}/history?range=` (raw price points when `range` is omitted)
  - Stock analytics (`/market/stocks/{id}/analytics`): volume/value 24h & 7d, 7d volatility, ownership count, active traders, market cap
  - Trending (`/market/trending`): most bought/sold, fastest rising/falling, highest volume
  - Leaderboards (`/leaderboards/{wealth,profit,traders}?period=`)
  - Country filter: `GET /market/stocks?country=XX` + `GET /market/countries` (distinct countries with per-country counts)
- Caching: Redis read-model cache on hot read paths (leaderboards, trending, market list/detail) with short TTLs and per-query keys
- Daily Login: 7-day cycle rewards with UTC-day idempotency (`/daily-login` claim + status; best-effort grant during osu! login)
- Notifications: holder fan-out on market events + list / unread filter / mark-read / mark-all-read
- Avatars & country codes: surfaced on market, leaderboard, activity-feed, and `/auth/me` payloads
- Admin: market settings management (multipliers + maintenance mode toggle)
- Background jobs (Hangfire/Worker): 4-tier osu sync (1m/5m/15m + hourly long tail) with token-bucket rate limiting + bounded concurrency, daily inactivity decay (03:00), daily wealth-snapshot capture (02:30), daily snapshot retention (04:00) and market-history retention (04:30)
- Security: CORS, rate limiting, global exception handler, concurrency conflict handling (HTTP 409), anti-abuse (trade cooldown, position limits, rapid trading detection)
- Deployment: Docker Compose (API + Worker + frontend + PostgreSQL + Redis + Caddy auto-TLS + nightly backups + Prometheus/Grafana) — see `deploy/README.md`

## Tech Stack

- .NET 9 / ASP.NET Core 9
- Entity Framework Core 9 + Npgsql (PostgreSQL)
- Redis (Microsoft.Extensions.Caching.StackExchangeRedis)
- Hangfire + Hangfire.PostgreSql
- MediatR + FluentValidation
- Mapster
- Swagger (Swashbuckle)
- Docker Compose + Caddy (auto-TLS reverse proxy)

## Requirements

- .NET SDK 9.0+
- PostgreSQL 16+ (or compatible)
- Redis 7+
- Docker Desktop (recommended for local infra and deployment)
- An osu! OAuth application (client id and client secret)

## Project Structure

```text
.
|-- src/
|   |-- Api/            # Minimal API endpoints, auth, Swagger, Hangfire dashboard
|   |-- Application/    # CQRS handlers, validators, behaviors, event handlers
|   |-- Domain/         # Entities, enums, repository contracts, domain services/events
|   |-- Infrastructure/ # EF Core, repositories, OAuth/Osu API clients, options providers
|   |-- Worker/         # Background host with Hangfire server enabled
|   `-- Shared/         # Shared contracts/helpers
|-- tests/
|   |-- OsuStocks.Api.IntegrationTests/
|   `-- OsuStocks.Application.UnitTests/
|-- nginx/              # Reverse proxy config for the local dev compose
|-- deploy/             # Production: Caddyfile, deploy.sh, observability, runbook
|-- docker-compose.yml      # Local dev stack
|-- docker-compose.prod.yml # Production stack (api/worker/web/db/redis/caddy/backups/metrics)
|-- OsuStocks.sln
`-- README.md
```

## Configuration

Default configuration files:

- `src/Api/appsettings.json`
- `src/Api/appsettings.Development.json`
- `src/Worker/appsettings.json`
- `src/Worker/appsettings.Development.json`

Key sections:

- `ConnectionStrings:Postgres`
- `ConnectionStrings:Redis`
- `OsuOAuth:*`
- `OsuApi:BaseUrl`
- `Jwt:*`
- `MarketEngine:*`
- `AntiAbuse:*`
- `Cors:AllowedOrigins` (array of allowed CORS origins)

`MarketEngine` keys:

- `TradeBuyImpactPerShare`
- `TradeSellImpactPerShare`
- `TopPlayImpactScale`, `MaxTopPlayImpact`, `MinTopPlayImpact` (top-play impact scales with play pp ÷ player pp, clamped)
- `PpImpactPerPoint`, `MaxPpImpact` (symmetric: applies to pp gains and losses)
- `RankChangeImpactScale`, `MaxRankChangeImpact` (bidirectional rank-change impact)
- `InactivityDecayImpact`
- `InactivityThresholdDays`
- `PriceFloor`

Do not commit secrets. Use user-secrets for local development:

```powershell
dotnet user-secrets set --project src/Api/OsuStocks.Api.csproj "OsuOAuth:ClientId" "<your-client-id>"
dotnet user-secrets set --project src/Api/OsuStocks.Api.csproj "OsuOAuth:ClientSecret" "<your-client-secret>"
dotnet user-secrets set --project src/Api/OsuStocks.Api.csproj "Jwt:SigningKey" "<min-32-char-random-secret>"
```

Important: `OsuOAuth:RedirectUri` must exactly match the callback URL registered in your osu OAuth app.

## Production Deployment

Production runs on a single host from `docker-compose.prod.yml`: **api, worker, web**
(the Next.js frontend from the sibling `OsuStocks.Web` repo), **postgres, redis**,
**caddy** (auto-TLS reverse proxy, the only service publishing host ports), a
**db-backup** sidecar (nightly `pg_dump`), and the **Prometheus/Grafana** stack.
TLS and routing are handled by Caddy (see `deploy/Caddyfile`); the canonical site is
the apex `osustocks.com`, the API is `api.osustocks.com`.

Full first-time runbook + DNS/secrets/observability: **`deploy/README.md`**.

### Day-to-day deploys

From the server, one command pulls both repos, rebuilds, and health-checks:

```bash
./deploy/deploy.sh             # pull + rebuild api/worker/web + up -d + /health check
./deploy/deploy.sh --migrate   # also apply EF migrations (when a merged PR adds one)
./deploy/deploy.sh --caddy     # also force-recreate caddy (after editing deploy/Caddyfile)
```

Configure all secrets via `.env` (see `deploy/README.md`); never commit it.

### Manual Deployment

Production security hardening is enforced for non-development environments:

- Hangfire dashboard requires authenticated Admin role and HTTPS.
- Swagger is disabled outside Development by default.
- JWT metadata requires HTTPS outside Development.
- Startup validates required production secrets from environment variables.
- CORS restricts cross-origin requests to configured origins.
- Rate limiting protects auth (10 req/min) and trading (30 req/min) endpoints.
- Global exception handler prevents information leakage.
- Concurrency conflicts return HTTP 409.

## Run Locally

1. Restore and build:

```powershell
dotnet restore OsuStocks.sln
dotnet build OsuStocks.sln
```

2. Start infrastructure (Docker example):

```powershell
docker run -d --name osu-stocks-postgres -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=osu_stocks_dev -p 5432:5432 postgres:16-alpine
docker run -d --name osu-stocks-redis -p 6379:6379 redis:7-alpine
```

3. Apply EF Core migration:

```powershell
dotnet ef database update --context AppDbContext --project src/Infrastructure/OsuStocks.Infrastructure.csproj --startup-project src/Api/OsuStocks.Api.csproj
```

4. Run API:

```powershell
dotnet run --project src/Api/OsuStocks.Api.csproj
```

5. Run Worker (separate terminal):

```powershell
dotnet run --project src/Worker/OsuStocks.Worker.csproj
```

6. Open tools:

- Swagger UI: `http://localhost:5152/swagger` (Development by default; set `Security__EnableSwagger=true` to enable outside Development)
- Hangfire Dashboard: `http://localhost:5152/hangfire` (requires Admin role; non-development access also requires HTTPS)
- Health: `GET /api/v1/health`

## API Coverage (Current)

Implemented route groups (all under `/api/v1`):

- `/auth` — `/login`, `/callback`, `/me` (rate limited: 10 req/min)
- `/market` — overview
- `/market/stocks` — list (`?search=`, `?sort=`, `?country=`, paged), `/{id}` details (incl. global rank + pp), `/{id}/history?range=` (OHLC candles, or raw points without `range`), `/{id}/analytics`, `/{id}/top-plays`
- `/market/countries` — distinct tracked countries with per-country counts
- `/market/movers` — live top movers
- `/market/events` — global activity feed, `/{stockId}` per-stock feed
- `/market/trending` — most bought/sold, fastest rising/falling, highest volume
- `/leaderboards` — `/wealth`, `/profit`, `/traders` (`?period=`)
- `/daily-login` — `/claim`, status (7-day cycle rewards)
- `/trading` — `/buy`, `/sell`, `/history` (rate limited: 30 req/min)
- `/portfolio` — summary, `/holdings`
- `/wallet` — balance, `/transactions`
- `/notifications` — list (`?unread=`), `/{id}/read`, `/read-all`
- `/admin/tracked-players` — list/search/add/enable/disable (Admin role)
- `/admin/market-settings` — get/update (Admin role)
- `/health`

The Hangfire dashboard is served at `/hangfire` (Admin role; HTTPS outside Development).

## Testing

Unit tests:

```powershell
dotnet test tests/OsuStocks.Application.UnitTests/OsuStocks.Application.UnitTests.csproj
```

Integration tests (PostgreSQL Testcontainers):

- Require Docker (local or CI runner).
- No local PostgreSQL instance is required.

```powershell
dotnet test tests/OsuStocks.Api.IntegrationTests/OsuStocks.Api.IntegrationTests.csproj
```

## CI/CD

GitHub Actions workflows in `.github/workflows/`:

| Workflow | Trigger | Description |
|----------|---------|-------------|
| `build.yml` | Push to `main`, PRs | Restore, build, unit tests, integration tests |
| `docker.yml` | Push to `main`, PRs (src changes) | Build API and Worker Docker images |
| `release.yml` | Tag `v*` | Publish production artifacts, push images to GHCR |

Release a new version:

```bash
git tag v1.0.0
git push origin v1.0.0
```

This builds production artifacts for both API and Worker, and pushes Docker images to GitHub Container Registry. The image names derive from `github.repository` (owner/repo), so for this repo they are `ghcr.io/zidanejidan12/osustocks.api/api` and `ghcr.io/zidanejidan12/osustocks.api/worker` (each tagged with the version and `latest`).

## Contribution Guide

> Detailed design docs (architecture, coding standards, API spec, domain model, etc.) are maintained locally and are not published to this repository.

1. Keep Clean Architecture boundaries strict (`Domain ← Application ← Api`; `Domain ← Infrastructure`).
2. Follow vertical-slice implementation style:
   - Command/Query
   - Handler
   - Validator
   - Endpoint
3. Use MediatR + FluentValidation + Result pattern for use cases.
4. For DB changes:
   - Update entities/configurations
   - Add migration in `src/Infrastructure/Persistence/Migrations`
5. Before PR:

```powershell
dotnet build OsuStocks.sln
dotnet test OsuStocks.sln
```
