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
  - Inputs: `BuyOrderExecuted`, `SellOrderExecuted`, `TopPlayDetected`, `PpIncreased`, `PlayerInactive`
  - Output: `PriceChanged`
  - Coefficient-based pricing + price floor + stock price history recording
- Market Intelligence (Phase 2):
  - Market activity event feed: global (`/market/events`) and per-stock (`/market/events/{stockId}`)
  - OHLC candles via `GET /market/stocks/{id}/history?range=` (raw price points when `range` is omitted)
  - Stock analytics (`/market/stocks/{id}/analytics`): volume/value 24h & 7d, 7d volatility, ownership count, active traders, market cap
  - Trending (`/market/trending`): most bought/sold, fastest rising/falling, highest volume
  - Leaderboards (`/leaderboards/{wealth,profit,traders}?period=`)
- Notifications: holder fan-out on market events + list / unread filter / mark-read / mark-all-read
- Avatars & country codes: surfaced on market, leaderboard, activity-feed, and `/auth/me` payloads
- Admin: market settings management (multipliers + maintenance mode toggle)
- Background jobs (Hangfire/Worker): tiered osu sync (1m/5m/15m), daily inactivity decay (03:00), daily wealth-snapshot capture (02:30)
- Security: CORS, rate limiting, global exception handler, concurrency conflict handling (HTTP 409), anti-abuse (trade cooldown, position limits, rapid trading detection)
- Deployment: Docker (API + Worker + PostgreSQL + Redis + nginx)

## Tech Stack

- .NET 9 / ASP.NET Core 9
- Entity Framework Core 9 + Npgsql (PostgreSQL)
- Redis (Microsoft.Extensions.Caching.StackExchangeRedis)
- Hangfire + Hangfire.PostgreSql
- MediatR + FluentValidation
- Mapster
- Swagger (Swashbuckle)
- Docker + nginx

## Requirements

- .NET SDK 9.0+
- PostgreSQL 16+ (or compatible)
- Redis 7+
- Docker Desktop (recommended for local infra and deployment)
- An osu! OAuth application (client id and client secret)

## Project Structure

```text
.
|-- docs/
|   |-- ARCHITECTURE.md
|   |-- API_SPEC.md
|   |-- BACKEND_ACCEPTANCE_TESTS.md
|   |-- BUSINESS_RULES.md
|   |-- CODING_STANDARDS.md
|   |-- DATABASE.md
|   |-- DEPLOYMENT.md
|   |-- DOMAIN_MODEL.md
|   |-- FRONTEND_API_CONTRACT.md
|   |-- OPERATIONS.md
|   |-- PHASE2_MARKET_INTELLIGENCE_PLAN.md
|   |-- ROADMAP.md
|   `-- USE_CASES.md
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
|-- nginx/              # Reverse proxy configuration
|-- docker-compose.yml  # Full stack deployment
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
- `TopPlayImpact`
- `PpImpactPerPoint`
- `MaxPpImpact`
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

### Docker Compose (recommended)

```bash
# Create .env with production secrets (see docs/DEPLOYMENT.md)
docker compose up -d --build
```

Services started: api, worker, postgres, redis, nginx.

See `docs/DEPLOYMENT.md` for full setup, environment variables, and TLS configuration.

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
- `/market/stocks` — list, `/{id}` details, `/{id}/history?range=` (OHLC candles, or raw points without `range`), `/{id}/analytics`
- `/market/events` — global activity feed, `/{stockId}` per-stock feed
- `/market/trending` — most bought/sold, fastest rising/falling, highest volume
- `/leaderboards` — `/wealth`, `/profit`, `/traders` (`?period=`)
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

## QA Acceptance Checklist

Use this document for milestone verification without reading source code:

- `docs/BACKEND_ACCEPTANCE_TESTS.md`

## Contribution Guide

1. Read all docs in `docs/` before coding, especially:
   - `docs/ARCHITECTURE.md`
   - `docs/CODING_STANDARDS.md`
   - `docs/API_SPEC.md`
2. Keep Clean Architecture boundaries strict.
3. Follow vertical-slice implementation style:
   - Command/Query
   - Handler
   - Validator
   - Endpoint
4. Use MediatR + FluentValidation + Result pattern for use cases.
5. For DB changes:
   - Update entities/configurations
   - Add migration in `src/Infrastructure/Persistence/Migrations`
6. Before PR:

```powershell
dotnet build OsuStocks.sln
dotnet test OsuStocks.sln
```
