# Environment Variables

Every configuration value the API and Worker read at runtime, with type, where it's required, and how it's validated.

> **Source of truth:** `src/Api/Program.cs` (`ValidateProductionSecretEnvironmentVariables`), `src/Worker/Program.cs`, `src/Infrastructure/DependencyInjection.cs`, and `docker-compose.yml`.
> **Secrets live only in `.env`** (gitignored). Use `.env.example` as the template. Never commit real secrets.

## Naming

.NET maps the `__` (double underscore) separator to nested config sections, e.g. `ConnectionStrings__Postgres` → `ConnectionStrings:Postgres`. Array indices use `__0`, `__1`, …

## Fail-fast in Production

When `ASPNETCORE_ENVIRONMENT=Production` (API) / `DOTNET_ENVIRONMENT=Production` (Worker), startup **aborts** if any required variable is missing, and additionally if `ConnectionStrings:Postgres`, `OsuOAuth:ClientSecret`, or `Jwt:SigningKey` are empty or still contain a `replace-with-` placeholder. A misconfigured container exits immediately rather than running insecurely.

---

## Required — API **and** Worker

| Variable | Example | Notes |
|---|---|---|
| `ConnectionStrings__Postgres` | `Host=postgres;Port=5432;Database=osu_stocks;Username=osu_stocks;Password=<STRONG>` | Npgsql connection string. The sole source of truth — must point at the production DB. |
| `ConnectionStrings__Redis` | `redis:6379` (or `redis:6379,password=<STRONG>`) | StackExchange.Redis format. Cache + rate-limit + OAuth state store. |
| `OsuOAuth__ClientId` | `12766` | From your osu! OAuth application. |
| `OsuOAuth__ClientSecret` | `hzaMvF1…` | **Secret.** From your osu! OAuth application. |
| `Jwt__Issuer` | `osu-stocks-prod` | Must match between API (issues) and any verifier. |
| `Jwt__Audience` | `osu-stocks-client` | JWT audience claim. |
| `Jwt__SigningKey` | `<48+ random chars>` | **Secret.** Min **32 chars** or startup fails. Generate with `openssl rand -base64 48`. |

## Required — API only

| Variable | Example | Notes |
|---|---|---|
| `OsuOAuth__RedirectUri` | `https://api.yourdomain.com/api/v1/auth/callback` | Must **exactly** match the callback URL registered in the osu! OAuth app, including scheme and path. |

## Required for correct production behaviour

| Variable | Example | Notes |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` | API. Enables the security hardening + secret validation. Anything other than `Development` is treated as production. |
| `DOTNET_ENVIRONMENT` | `Production` | **Worker** equivalent (the Worker is not an ASP.NET host). In `docker-compose.yml` this is wired from `${ASPNETCORE_ENVIRONMENT}`. |
| `Cors__AllowedOrigins__0` | `https://app.yourdomain.com` | Frontend origin allowed to call the API from a browser. Add `__1`, `__2`, … for more. Unlisted origins are blocked by the browser. |
| `Security__OAuthReturnUrl__AllowedOrigins__0` | `https://app.yourdomain.com` | Allow-list for `?returnUrl=` on `/auth/login`. Unknown origins → `400 VALIDATION_ERROR`. (loopback only allowed in Development.) |

## Optional

| Variable | Default | Notes |
|---|---|---|
| `Security__EnableSwagger` | `false` | Set `true` to expose Swagger UI at `/swagger` outside Development. Leave unset/false in production unless temporarily needed. |
| `Jwt__ExpirationMinutes` | `120` | Access-token lifetime in minutes. |

## Compose-level (`.env`, consumed by `docker-compose.yml`)

These are not read by the app directly; `docker-compose.yml` interpolates them into the variables above and into the database container.

| Variable | Example | Notes |
|---|---|---|
| `POSTGRES_PASSWORD` | `<STRONG>` | Sets the Postgres superuser password **and** is interpolated into `ConnectionStrings__Postgres`. ⚠️ Only applied on **first** volume init — changing it later requires re-init or an `ALTER ROLE`. |
| `OSUOAUTH_CLIENT_ID` / `OSUOAUTH_CLIENT_SECRET` / `OSUOAUTH_REDIRECT_URI` | — | Feed the `OsuOAuth__*` vars. |
| `JWT_ISSUER` / `JWT_AUDIENCE` / `JWT_SIGNING_KEY` | — | Feed the `Jwt__*` vars. |
| `CORS_ORIGIN` | `https://app.yourdomain.com` | Feeds `Cors__AllowedOrigins__0` and `Security__OAuthReturnUrl__AllowedOrigins__0`. |
| `RETENTION_DAYS` | `30` | Backup retention (read by `scripts/pg-backup.sh`). |
| `BACKUP_DIR` | `/backups` | Backup path inside the postgres container. |

---

## Production `.env` template

```dotenv
# ---- Runtime ----
ASPNETCORE_ENVIRONMENT=Production

# ---- PostgreSQL ----
POSTGRES_PASSWORD=replace-with-a-strong-database-password
ConnectionStrings__Postgres=Host=postgres;Port=5432;Database=osu_stocks;Username=osu_stocks;Password=replace-with-a-strong-database-password

# ---- Redis ----
ConnectionStrings__Redis=redis:6379

# ---- osu! OAuth (https://osu.ppy.sh/home/account/edit#oauth) ----
OsuOAuth__ClientId=
OsuOAuth__ClientSecret=
OsuOAuth__RedirectUri=https://api.yourdomain.com/api/v1/auth/callback

# ---- JWT ----  (generate key: openssl rand -base64 48)
Jwt__Issuer=osu-stocks-prod
Jwt__Audience=osu-stocks-client
Jwt__SigningKey=

# ---- CORS / return-url allow-list (frontend origin) ----
Cors__AllowedOrigins__0=https://app.yourdomain.com
Security__OAuthReturnUrl__AllowedOrigins__0=https://app.yourdomain.com

# ---- Optional ----
Security__EnableSwagger=false
```

> If you deploy via `docker-compose.yml` as written, set the **compose-level** names (`POSTGRES_PASSWORD`, `OSUOAUTH_CLIENT_ID`, `JWT_SIGNING_KEY`, `CORS_ORIGIN`, …) instead — compose maps them to the `__`-style names above. Running the binaries directly (no compose) requires the `__`-style names exactly as listed.
