# Changes — `feature/tix-fix-lmaoaoao` (OsuStocks.API)

Backend changes for two goals: **stop hitting the osu! API rate limit so often**, and
**stop forcing users to re-login every ~15 minutes** (refresh-token rotation). Plus
**edge hardening** at the Caddy reverse proxy. Companion frontend changes live in the
sibling `OsuStocks.Web` repo (see its `CHANGES.md`).

> Built on top of PR #49 ("partition rate limiters per client IP and exempt /me"), which
> moved the ASP.NET limiters to per-IP partitioned policies via a `GetClientIp` helper.

---

## 1. osu! rate-limiting (fewer 429s)

The osu! app has one per-process request budget. Previously the OAuth token endpoint
bypassed the throttle entirely, the client-credentials token was re-fetched every sync
cycle, there was no 429 backoff anywhere, and the API + Worker each ran a 600/min limiter
(≈1200/min combined, at osu!'s ceiling).

| File | Change |
|------|--------|
| `src/Infrastructure/OsuIntegration/Api/OsuApiRetryHandler.cs` *(new)* | A `DelegatingHandler` that retries transient osu! responses (429, 500, 502, 503, 504) up to 3 attempts. Honors the `Retry-After` header, else exponential backoff + jitter (capped 10s). Buffers/clones the request so POSTs can be safely re-sent. Logs each retry at WARN. |
| `src/Infrastructure/DependencyInjection.cs` | Registered `OsuApiRetryHandler`; attached **retry (outer) + rate-limit (inner)** handlers to **both** osu! HTTP clients — including the OAuth client, which previously had no handlers, so `/oauth/token` now goes through the same throttle queue. Added a 15s `HttpClient.Timeout` to both (replaces the 100s default). Registered `IRefreshTokenService` (see §2). |
| `src/Infrastructure/OsuIntegration/OAuth/OsuOAuthService.cs` | `GetClientCredentialsTokenAsync` now **caches the token in Redis** (`IDistributedCache`, key `osu:client-token`), reusing it while >60s remain. Eliminates a `/oauth/token` POST on every sync cycle (it has 4 callers). Cache failures fall back to a live fetch. |
| `src/Infrastructure/OsuIntegration/Api/OsuApiClient.cs` + `src/Domain/OsuIntegration/Interfaces/IOsuApiClient.cs` | `GetCurrentUserAsync` gained an optional `includeTopScore` param so login can skip the top-score call (it never uses it). Halves osu! calls per sign-in. |
| `src/Application/Features/OsuIntegration/Auth/HandleOsuCallback/HandleOsuCallbackCommandHandler.cs` | Login calls `GetCurrentUserAsync(..., includeTopScore: false, ...)`. Also issues a refresh token (see §2). |
| `src/Infrastructure/OsuIntegration/Api/OsuApiRateLimitingHandler.cs` | Added a WARN log when the throttle queue is full (was a silent throw); injected `ILogger`. |
| `src/Api/appsettings.json`, `src/Api/appsettings.Development.json` | `OsuApi:RequestsPerMinute` 600 → **120** (API process). |
| `src/Worker/appsettings.json`, `src/Worker/appsettings.Development.json` | `OsuApi:RequestsPerMinute` 600 → **300** (Worker process, which does the heavy syncing). |

All values are tunable via config; watch the existing `osu_api.*` telemetry / Grafana to retune.

---

## 2. Long sessions — refresh-token rotation

The access JWT stays short-lived (60 min). A new long-lived, single-use **rotating refresh
token** lets the SPA mint fresh access tokens silently, so an active session is never bounced
back through osu! OAuth.

| File | Change |
|------|--------|
| `src/Application/Common/Interfaces/IRefreshTokenService.cs` *(new)* | Interface: `IssueAsync(userId)` and `ValidateAndRotateAsync(token)` (consume + reissue). Returns `RefreshTokenResult` / `RefreshTokenRotation`. |
| `src/Infrastructure/Authentication/RedisRefreshTokenService.cs` *(new)* | Redis (`IDistributedCache`) implementation. 256-bit URL-safe token; only its **SHA-256 hash** is stored (key `auth:refresh:<hash>` → userId); **30-day sliding TTL** renewed on each rotation; validation deletes the old key before issuing a replacement (single-use). |
| `src/Application/Features/OsuIntegration/Auth/RefreshToken/RefreshTokenCommand.cs` + `RefreshTokenCommandHandler.cs` + `RefreshTokenResponse.cs` *(new)* | MediatR command: validate+rotate the refresh token, load the user, mint a new JWT, return `{ accessToken, expiresAt, refreshToken, refreshExpiresAt }`. Invalid/expired/used → `INVALID_REFRESH_TOKEN`. |
| `src/Application/Features/OsuIntegration/Auth/HandleOsuCallback/HandleOsuCallbackResponse.cs` | Added `RefreshToken` + `RefreshExpiresAt` fields. |
| `src/Application/Features/OsuIntegration/Auth/HandleOsuCallback/HandleOsuCallbackCommandHandler.cs` | Issues a refresh token on successful login and returns it. |
| `src/Api/Endpoints/AuthEndpoints.cs` | Callback redirect fragment + JSON now carry `refreshToken`/`refreshExpiresAt`. New **`POST /api/v1/auth/refresh`** (anonymous — the access token may be expired; the refresh token is the credential) under a new `auth-refresh` rate-limit policy. Added a `RefreshTokenRequest` body record. |
| `src/Api/Program.cs` | New per-IP **`auth-refresh`** rate-limit policy (60/min/IP), following PR #49's `AddPolicy` + `RateLimitPartition` + `GetClientIp` pattern. |
| `src/Api/Common/ResultHttpMapper.cs` | `INVALID_REFRESH_TOKEN` → HTTP 401. |

---

## 3. Edge hardening at Caddy (production only)

The prod edge is Caddy (`docker-compose.prod.yml`), previously a bare `reverse_proxy`.

| File | Change |
|------|--------|
| `deploy/Caddy.Dockerfile` *(new)* | Custom Caddy build via `xcaddy` with `caddy-ratelimit` + Coraza (OWASP CRS) WAF plugins (stock Caddy has neither). |
| `deploy/Caddyfile` | **Global per-IP rate limit** (api 120/min, web 240/min), **1 MB body cap** + 16 KB header cap, **Coraza WAF in `DetectionOnly`** (logs, doesn't block), and **bad-bot / empty-User-Agent 403s**. Grafana left un-hardened (own auth; WAF can break its API/websockets). |
| `docker-compose.prod.yml` | `caddy` service switched from `image: caddy:2-alpine` to a `build:` of `deploy/Caddy.Dockerfile`. |
| `deploy/deploy.sh` | `--caddy` now **rebuilds** the custom image and force-recreates the container. |
| `deploy/README.md` | New "Edge hardening (Caddy)" section documenting the knobs and how to flip the WAF from `DetectionOnly` to `On`. |

> **Prod-only, validate on the server:** these can't be built/run without Docker.
> `caddy validate --config deploy/Caddyfile --adapter caddyfile`, deploy with
> `deploy/deploy.sh --caddy`, then flip `SecRuleEngine DetectionOnly` → `On` once the
> WAF logs are free of false positives.

---

## 4. Tests

| File | Change |
|------|--------|
| `tests/OsuStocks.Api.IntegrationTests/RefreshTokenEndpointsTests.cs` *(new)* | Covers `/auth/refresh`: valid token → rotated token + valid JWT; replayed (old) token → 401; rotated token → 200; unknown token → 401. |
| `tests/.../Infrastructure/InMemoryRefreshTokenService.cs` *(new)* | In-memory `IRefreshTokenService` double so integration tests don't need Redis. |
| `tests/.../Infrastructure/CustomWebApplicationFactory.cs`, `PostgresWebApplicationFactory.cs` | Register the in-memory refresh-token double. |
| `tests/.../Infrastructure/FakeOsuApiClient.cs`, `SynchronizationWorkerIntegrationTests.cs`, `tests/OsuStocks.Application.UnitTests/.../SeedTopPlayersCommandHandlerTests.cs` | Updated `GetCurrentUserAsync` doubles for the new `includeTopScore` parameter. |

### Verification (this machine)
- `dotnet build OsuStocks.sln` — clean (1 pre-existing CS8604 warning in `AdminEndpoints.cs`).
- Unit tests: **136/136**. Auth/refresh/rate-limit integration tests: **8/8**.
- Note: the net9.0 runtime isn't installed locally — run tests with `DOTNET_ROLL_FORWARD=Major dotnet test ...`. The Testcontainers Postgres suite needs Docker (CI only).
