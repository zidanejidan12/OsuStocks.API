# OsuStocks.API

Backend service for the osu! Stocks game platform. This repository currently contains the Clean Architecture foundation, persistence layer, and the Osu Integration authentication and synchronization skeleton.

## Current Status

- Implemented: solution skeleton (Api, Application, Domain, Infrastructure, Worker, Shared)
- Implemented: EF Core + PostgreSQL mappings and initial migration
- Implemented: Redis cache wiring, Hangfire wiring, MediatR, FluentValidation, Mapster
- Implemented: Osu OAuth login/callback flow and user profile endpoint
- Not implemented yet: market engine, trading, wallet, and other gameplay business logic

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
|   |-- BUSINESS_RULES.md
|   |-- CODING_STANDARDS.md
|   |-- DATABASE.md
|   |-- DOMAIN_MODEL.md
|   |-- ROADMAP.md
|   `-- USE_CASES.md
|-- src/
|   |-- Api/            # Minimal API endpoints, auth, Swagger, Hangfire dashboard
|   |-- Application/    # CQRS handlers, validators, pipeline behaviors, abstractions
|   |-- Domain/         # Entities, enums, repository contracts, domain interfaces/events
|   |-- Infrastructure/ # EF Core, repositories, OAuth/Osu API clients, token storage
|   |-- Worker/         # Background host with Hangfire server enabled
|   `-- Shared/         # Shared contracts/helpers (currently minimal)
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

Do not commit secrets. Use user-secrets for local development:

```powershell
dotnet user-secrets set --project src/Api/OsuStocks.Api.csproj "OsuOAuth:ClientId" "<your-client-id>"
dotnet user-secrets set --project src/Api/OsuStocks.Api.csproj "OsuOAuth:ClientSecret" "<your-client-secret>"
dotnet user-secrets set --project src/Api/OsuStocks.Api.csproj "Jwt:SigningKey" "<min-32-char-random-secret>"
```

Important: `OsuOAuth:RedirectUri` must exactly match the callback URL registered in your osu! OAuth app.

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

- Swagger UI: `http://localhost:5152/swagger` (or HTTPS profile URL)
- Hangfire Dashboard: `http://localhost:5152/hangfire`
- Health: `GET /api/v1/health`

## OAuth Flow Notes

- `GET /api/v1/auth/login?returnUrl=http://localhost:3000/dashboard` returns an HTTP redirect to osu! authorization.
- In Swagger Try it out, redirect endpoints can show browser fetch/CORS style errors. Use direct browser navigation for login redirect tests.
- After osu callback, API returns app JWT (`accessToken`, `expiresAt`, `returnUrl`).

## Contribution Guide

1. Read and follow all documents in `docs/`, especially:
   - `docs/ARCHITECTURE.md`
   - `docs/CODING_STANDARDS.md`
   - `docs/API_SPEC.md`
2. Keep Clean Architecture boundaries strict:
   - Domain has no framework dependencies.
   - Application contains use-case orchestration, not business invariants.
   - Infrastructure implements contracts and external integrations.
3. Use the vertical-slice approach for new features:
   - Command/Query
   - Handler
   - Validator
   - Endpoint
4. Use MediatR + FluentValidation + Result pattern for new use cases.
5. For DB changes:
   - Update entity/configuration in Infrastructure.
   - Add migration in `src/Infrastructure/Persistence/Migrations`.
6. Before opening a PR, run:

```powershell
dotnet build OsuStocks.sln
dotnet test OsuStocks.sln
```

Note: test projects are not scaffolded yet, so `dotnet test` may report no tests discovered at this stage.
