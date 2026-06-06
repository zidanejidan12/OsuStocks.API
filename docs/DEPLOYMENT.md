# Deployment Guide

## Production Security Defaults

The API and Worker now enforce secure production behavior:

- Hangfire dashboard is protected by Admin role authorization.
- Hangfire dashboard access also requires HTTPS outside Development.
- Swagger is disabled by default outside Development.
- JWT metadata requires HTTPS outside Development.
- Required production secrets must be provided via environment variables.
- CORS is enforced; only origins listed in `Cors:AllowedOrigins` can make cross-origin requests.
- Rate limiting protects auth endpoints (10 req/min) and trading endpoints (30 req/min).
- Global exception handler prevents stack trace leakage and returns structured error responses.
- Optimistic concurrency conflicts return HTTP 409 with `CONCURRENCY_CONFLICT` error code.

## Required Environment Variables

Set these for production API and Worker processes:

- `ConnectionStrings__Postgres`
- `ConnectionStrings__Redis`
- `OsuOAuth__ClientId`
- `OsuOAuth__ClientSecret`
- `Jwt__Issuer`
- `Jwt__Audience`
- `Jwt__SigningKey`

Additional API requirement:

- `OsuOAuth__RedirectUri`

Optional:

- `Security__EnableSwagger=true` to explicitly enable Swagger outside Development.
- `Cors__AllowedOrigins__0` (and `__1`, `__2`, etc.) for additional CORS origins.

## Startup Validation Behavior

When `ASPNETCORE_ENVIRONMENT=Production`:

- Startup fails if any required environment variable is missing.
- Startup fails if `ConnectionStrings:Postgres`, `OsuOAuth:ClientSecret`, or `Jwt:SigningKey` are empty or placeholder values.

This fail-fast behavior prevents accidental deployment with insecure defaults.

## CORS Configuration

Configure allowed frontend origins:

- `Cors:AllowedOrigins` accepts an array of allowed origins (scheme + host + optional port).
- Requests from unlisted origins are blocked by the browser.
- In development, `http://localhost:3000` and `https://localhost:3000` are configured by default.

## Rate Limiting

Two rate limiting policies are enforced:

- **auth**: 10 requests per minute per client on `/api/v1/auth/*` endpoints.
- **trading**: 30 requests per minute per client on `/api/v1/trading/*` endpoints.

Exceeded requests return HTTP 429 Too Many Requests.

## OAuth Return URL Allow-List

Configure trusted frontend origins for `GET /api/v1/auth/login?returnUrl=...`:

- `Security:OAuthReturnUrl:AllowedOrigins` accepts absolute origins (scheme + host + optional port).
- Unknown origins are rejected with `400 VALIDATION_ERROR`.
- `localhost` loopback origins are accepted only in `Development`.

## Docker Compose Deployment

The project includes Docker support for production deployment.

### Files

- `src/Api/Dockerfile` - Multi-stage build for the API
- `src/Worker/Dockerfile` - Multi-stage build for the Worker
- `docker-compose.yml` - Full stack: api, worker, postgres, redis, nginx
- `nginx/nginx.conf` - Reverse proxy configuration

### Quick Start

1. Create a `.env` file from the template and fill in your values:

```bash
cp .env.example .env
# Edit .env with your production secrets
```

See `.env.example` for all available variables and descriptions.

2. Build and start all services:

```bash
docker compose up -d --build
```

3. Apply EF Core migrations:

```bash
docker compose exec api dotnet OsuStocks.Api.dll -- --migrate
```

Or apply migrations manually before first deployment:

```bash
dotnet ef database update --context AppDbContext \
  --project src/Infrastructure/OsuStocks.Infrastructure.csproj \
  --startup-project src/Api/OsuStocks.Api.csproj \
  --connection "Host=localhost;Port=5432;Database=osu_stocks;Username=osu_stocks;Password=strong-production-password"
```

4. Verify health:

```bash
curl http://localhost/health
```

### Services

| Service | Port | Description |
|---------|------|-------------|
| nginx | 80 | Reverse proxy / TLS termination point |
| api | 8080 | ASP.NET Core API (internal) |
| worker | - | Hangfire background worker |
| postgres | 5432 | PostgreSQL 16 database |
| redis | 6379 | Redis 7 cache |

### TLS / HTTPS

For production HTTPS:

1. Obtain TLS certificates (e.g., Let's Encrypt via certbot).
2. Update `nginx/nginx.conf` to add an HTTPS server block with your certificates.
3. Set `X-Forwarded-Proto` header so ASP.NET Core correctly detects HTTPS.

## Example (PowerShell - without Docker)

API:

```powershell
$env:ASPNETCORE_ENVIRONMENT='Production'
$env:ConnectionStrings__Postgres='Host=db;Port=5432;Database=osu_stocks;Username=osu;Password=strong-password'
$env:ConnectionStrings__Redis='redis:6379,password=strong-redis-password'
$env:OsuOAuth__ClientId='your-client-id'
$env:OsuOAuth__ClientSecret='your-client-secret'
$env:OsuOAuth__RedirectUri='https://api.example.com/api/v1/auth/callback'
$env:Jwt__Issuer='osu-stocks-prod'
$env:Jwt__Audience='osu-stocks-client'
$env:Jwt__SigningKey='use-a-long-random-signing-key-at-least-32-characters'
$env:Cors__AllowedOrigins__0='https://app.example.com'
dotnet run --project src/Api/OsuStocks.Api.csproj
```

Worker:

```powershell
$env:ASPNETCORE_ENVIRONMENT='Production'
$env:ConnectionStrings__Postgres='Host=db;Port=5432;Database=osu_stocks;Username=osu;Password=strong-password'
$env:ConnectionStrings__Redis='redis:6379,password=strong-redis-password'
$env:OsuOAuth__ClientId='your-client-id'
$env:OsuOAuth__ClientSecret='your-client-secret'
$env:Jwt__Issuer='osu-stocks-prod'
$env:Jwt__Audience='osu-stocks-client'
$env:Jwt__SigningKey='use-a-long-random-signing-key-at-least-32-characters'
dotnet run --project src/Worker/OsuStocks.Worker.csproj
```

## Backups & Disaster Recovery

PostgreSQL is the sole persistent store. Redis is ephemeral and does not require backup.

### Backup Scripts

Two scripts are included in the `scripts/` directory and mounted read-only into the `postgres` container:

| Script | Purpose |
|--------|---------|
| `scripts/pg-backup.sh` | Creates a compressed `pg_dump` backup with automatic retention pruning |
| `scripts/pg-restore.sh` | Drops and recreates the database from a backup file |

### Running a Backup

```bash
docker compose exec postgres /scripts/pg-backup.sh
```

Backups are stored in the `postgres_backups` Docker volume at `/backups/` inside the container.

### Scheduling Daily Backups

Add a cron entry on the Docker host:

```bash
0 2 * * * cd /path/to/project && docker compose exec -T postgres /scripts/pg-backup.sh >> /var/log/osu-stocks-backup.log 2>&1
```

### Restoring from Backup

**WARNING**: This drops and recreates the database.

```bash
docker compose stop api worker
docker compose exec postgres /scripts/pg-restore.sh /backups/osu_stocks_20260606_020000.sql.gz
docker compose start api worker
```

### Configuration

| Variable | Default | Description |
|----------|---------|-------------|
| `RETENTION_DAYS` | 30 | Days to keep backups before pruning |
| `BACKUP_DIR` | `/backups` | Backup storage path inside container |

For full disaster recovery procedures, restore-to-new-host instructions, and recovery time objectives, see `docs/DISASTER_RECOVERY.md`.

## Operational Notes

- Keep `Security__EnableSwagger` unset in production unless there is a temporary operational need.
- Restrict Admin role assignment carefully; it now governs Hangfire dashboard access.
- Use TLS termination and forward HTTPS correctly so `Request.IsHttps` remains true for dashboard access.
- Add `.env` to `.gitignore` to prevent committing production secrets (already configured).
- Use `.env.example` as the template; it contains all variables with placeholder values and documentation.
- Copy backups to off-host storage regularly; Docker volumes alone do not protect against host failure.
