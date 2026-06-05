# OsuStocks.API

Backend service for the osu! Stocks game platform.

This repository uses a modular monolith with Clean Architecture and vertical slices. Current implementation includes authentication, player registry, synchronization, trading, portfolio, and market engine foundations.

## Current Status

Implemented modules:

- Solution skeleton: `Api`, `Application`, `Domain`, `Infrastructure`, `Worker`, `Shared`
- Persistence: EF Core + PostgreSQL mappings and initial migration
- Infrastructure wiring: Redis cache, Hangfire, MediatR, FluentValidation, Mapster, Swagger
- Authentication: osu OAuth login/callback + JWT issuance + `/auth/me`
- Player Registry: add/list/search/enable/disable tracked players
- Synchronization: tracked-player snapshot sync + market event persistence
- Trading: buy/sell/history
- Portfolio: holdings and portfolio summary
- Market Engine:
  - Inputs: `BuyOrderExecuted`, `SellOrderExecuted`, `TopPlayDetected`, `PpIncreased`, `PlayerInactive`
  - Output: `PriceChanged`
  - Coefficient-based pricing + price floor + stock price history recording

Planned / partial:

- Full market read endpoints (`/market/*`) from API spec are not fully exposed yet.
- Wallet dedicated API endpoints (`/wallet`, `/wallet/transactions`) are still pending.

## Tech Stack

- .NET 9 / ASP.NET Core 9
- Entity Framework Core 9 + Npgsql (PostgreSQL)
- Redis (Microsoft.Extensions.Caching.StackExchangeRedis)
- Hangfire + Hangfire.PostgreSql
- MediatR + FluentValidation
- Mapster
- Swagger (Swashbuckle)

## Requirements

- .NET SDK 9.0+
- PostgreSQL 16+ (or compatible)
- Redis 7+
- Docker Desktop (optional but recommended for local infra)
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
|   |-- DOMAIN_MODEL.md
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

`MarketEngine` keys:

- `TradeBuyImpactPerShare`
- `TradeSellImpactPerShare`
- `TopPlayImpact`
- `PpImpactPerPoint`
- `MaxPpImpact`
- `InactivityDecayImpact`
- `PriceFloor`

Do not commit secrets. Use user-secrets for local development:

```powershell
dotnet user-secrets set --project src/Api/OsuStocks.Api.csproj "OsuOAuth:ClientId" "<your-client-id>"
dotnet user-secrets set --project src/Api/OsuStocks.Api.csproj "OsuOAuth:ClientSecret" "<your-client-secret>"
dotnet user-secrets set --project src/Api/OsuStocks.Api.csproj "Jwt:SigningKey" "<min-32-char-random-secret>"
```

Important: `OsuOAuth:RedirectUri` must exactly match the callback URL registered in your osu OAuth app.

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

- Swagger UI: `http://localhost:5065/swagger` (or the URL shown in startup logs)
- Hangfire Dashboard: `http://localhost:5065/hangfire`
- Health: `GET /api/v1/health`

## API Coverage (Current)

Implemented route groups:

- `/api/v1/auth`
- `/api/v1/admin/tracked-players`
- `/api/v1/trading`
- `/api/v1/portfolio`
- `/api/v1/health`

Not fully implemented yet from `docs/API_SPEC.md`:

- `/api/v1/market/*`
- `/api/v1/wallet*`
- `/api/v1/leaderboards*`

## Testing

Run all tests:

```powershell
dotnet test OsuStocks.sln
```

Run key focused suites:

```powershell
dotnet test tests/OsuStocks.Application.UnitTests/OsuStocks.Application.UnitTests.csproj --filter "MarketPriceEngineTests|MarketEventProcessingServiceTests"
dotnet test tests/OsuStocks.Api.IntegrationTests/OsuStocks.Api.IntegrationTests.csproj --filter "TradingEndpointsTests|PortfolioEndpointsTests|OAuthCallbackEndpointsTests"
```

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
