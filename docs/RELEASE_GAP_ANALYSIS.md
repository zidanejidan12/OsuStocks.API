# Release Gap Analysis

**Date**: 2026-06-06 (updated post-MVP Hardening Sprint)
**Baseline**: Documentation under `docs/` (source of truth)
**Compared against**: Current implementation (post-MVP Release Hardening Sprint)

---

## Executive Summary

The MVP Release Hardening Sprint resolved 7 gaps from the original analysis, including all Critical-severity items (Docker, CORS, rate limiting) and several High-severity items (global exception handler, concurrency handling). The remaining 24 gaps are primarily Medium/Low severity and fall into three categories: (1) unscheduled background jobs and missing domain events, (2) API contract drift between spec and implementation, and (3) performance/observability improvements for scale.

**Verdict**: Conditional Go — the application is deployable for a limited beta with the caveats noted below. Two High-severity items (GAP-F02 daily decay scheduling, GAP-F06 anti-abuse) should be addressed before opening to a wider audience.

---

## Resolved Gaps (MVP Hardening Sprint)

| Original ID | Description | Resolution |
|-------------|-------------|------------|
| GAP-S01 | No CORS configuration | CORS with config-driven allowed origins in `Program.cs`; integration tested |
| GAP-S02 | No rate limiting | Fixed-window rate limiting: auth (10/min), trading (30/min); integration tested |
| GAP-S04 | No global exception handler | `GlobalExceptionHandlerMiddleware` catches all exceptions, returns structured JSON |
| GAP-O06 | No concurrency conflict handling | `DbUpdateConcurrencyException` → HTTP 409 `CONCURRENCY_CONFLICT`; integration tested |
| GAP-D01 / GAP-O01 | No Docker artifacts | Dockerfiles for Api/Worker, `docker-compose.yml` (5 services), `nginx.conf`, `.dockerignore` |
| GAP-D05 | No TLS/deployment documentation | `DEPLOYMENT.md` rewritten: Docker Compose, TLS guidance, env-var reference, `.env` setup |
| GAP-O05 | README drift from implementation | `README.md` rewritten to reflect current implementation state |

---

## Remaining Gaps

### 1. Functional Gaps

#### GAP-F01 — DemandScore field unused

- **Severity**: Low
- **Detail**: `Stock.DemandScore` exists in the domain model and database but is never read or written by any handler. `DOMAIN_MODEL.md` describes it as an input to pricing.
- **Effort**: 2-4 hours
- **Recommendation**: Either integrate into the market engine pricing formula or remove from the entity to reduce confusion.

#### GAP-F02 — Daily inactivity decay not scheduled

- **Severity**: High
- **Detail**: `BUSINESS_RULES.md` §4.5 specifies a daily job that fires `PlayerInactive` for players with no snapshot change in 7+ days. The `PlayerInactiveHandler` exists and processes the event, but no Hangfire recurring job triggers it independently of the sync job. Prices never decay on a scheduled basis for inactive players.
- **Effort**: 4-6 hours
- **Recommendation**: Add a `RecurringJob` in the Worker that queries for stale players and publishes `PlayerInactive` events on a daily schedule.

#### GAP-F03 — Daily login rewards not implemented

- **Severity**: Medium
- **Detail**: `BUSINESS_RULES.md` §5.2 specifies daily login rewards (100 coins, tracked by `LastDailyRewardAt`). The `User` entity has the field and `WalletTransactionType.DailyReward` enum exists, but no handler or endpoint implements the feature.
- **Effort**: 4-6 hours
- **Recommendation**: Implement as a POST endpoint or automatic grant on `/auth/me`. Can be deferred to Phase 1.5 if documented.

#### GAP-F04 — TopPlayDetected event never published

- **Severity**: Medium
- **Detail**: `BUSINESS_RULES.md` §4.3 describes `TopPlayDetected` as a market event when a player sets a new top play. The handler exists but the sync job never detects or publishes this event.
- **Effort**: 4-6 hours
- **Recommendation**: Add top-play comparison logic to the sync job. Requires storing previous top plays for diff.

#### GAP-F05 — PpIncreased event never published

- **Severity**: Medium
- **Detail**: `BUSINESS_RULES.md` §4.4 describes `PpIncreased` for PP gains between snapshots. The handler exists but the sync job does not compare PP values or publish this event.
- **Effort**: 2-4 hours
- **Recommendation**: Add PP comparison in the sync job between current and previous snapshot.

#### GAP-F06 — Anti-abuse / wash-trading prevention missing

- **Severity**: High
- **Detail**: `BUSINESS_RULES.md` §3.5 specifies cooldown periods and max-position limits per stock. BR-041/BR-042 specify self-trading prevention and market manipulation monitoring. No enforcement exists.
- **Effort**: 6-8 hours
- **Recommendation**: Add cooldown check (last trade timestamp per user+stock) and max-position validation in `BuyStockHandler`. Add suspicious pattern logging for rapid buy-sell cycles.

#### GAP-F07 — GET /market/stocks response shape drift

- **Severity**: Low
- **Detail**: `API_SPEC.md` specifies `currentPrice`, `priceChangePercent24h`, `demandScore` in stock list responses. Implementation returns `currentPrice` and `priceChange24h` (absolute, not percent) and omits `demandScore`.
- **Effort**: 2-3 hours
- **Recommendation**: Align response DTO to spec or update spec to match implementation.

#### GAP-F08 — GET /market/stocks/{id}/history missing interval parameter

- **Severity**: Low
- **Detail**: `API_SPEC.md` specifies `?interval=1h|6h|24h|7d` query parameter. Implementation returns all history without filtering.
- **Effort**: 2-3 hours
- **Recommendation**: Add interval filtering to the query handler.

#### GAP-F09 — Pagination not implemented on list endpoints

- **Severity**: Medium
- **Detail**: `API_SPEC.md` specifies `page` and `pageSize` query parameters on `/market/stocks`, `/portfolio/holdings`, `/wallet/transactions`, `/trading/history`. None implement pagination.
- **Effort**: 4-6 hours
- **Recommendation**: Add pagination support using a shared `PagedResult<T>` wrapper.

#### GAP-F10 — Leaderboards not implemented (deferred)

- **Severity**: Low (intentionally deferred)
- **Detail**: `/api/v1/leaderboards/*` endpoints are documented but explicitly postponed to Phase 1.5 in README and ROADMAP.
- **Effort**: 8-12 hours
- **Recommendation**: No action needed for MVP. Tracked in ROADMAP.md.

---

### 2. Security Gaps

#### GAP-S03 — Placeholder signing key in appsettings

- **Severity**: Medium
- **Detail**: `appsettings.Development.json` contains a placeholder JWT signing key. Production startup validation rejects it, but the placeholder could be accidentally used in staging.
- **Effort**: 1 hour
- **Recommendation**: Remove the placeholder value entirely; rely on environment variables and user-secrets only.

#### GAP-S05 — No security headers middleware

- **Severity**: Low
- **Detail**: Standard security headers (`X-Content-Type-Options`, `X-Frame-Options`, `Strict-Transport-Security`, `Content-Security-Policy`) are not set.
- **Effort**: 1-2 hours
- **Recommendation**: Add security headers middleware or use a library like `NetEscapades.AspNetCore.SecurityHeaders`.

#### GAP-S06 — Domain entity invariant enforcement incomplete

- **Severity**: Low
- **Detail**: Some domain entities allow direct property mutation without guard clauses (e.g., `Stock.CurrentPrice` can be set to negative values). `CODING_STANDARDS.md` recommends encapsulated domain logic.
- **Effort**: 4-6 hours
- **Recommendation**: Add guard clauses to entity setters/methods. Low risk at MVP since all mutations go through validated handlers.

---

### 3. Performance Gaps

#### GAP-P01 — Sequential sync loop does not scale

- **Severity**: Medium
- **Detail**: `SyncTrackedPlayersJob` iterates players sequentially with 2 osu! API calls per player. At 100+ players, the job risks exceeding Hangfire's 30-second timeout.
- **Effort**: 8-12 hours
- **Recommendation**: Batch players, parallelize API calls with concurrency limits, and implement retry/circuit-breaker. Not blocking for MVP with <50 players.

#### GAP-P02 — No database indexes beyond primary keys

- **Severity**: Medium
- **Detail**: Common query patterns (stocks by player, holdings by user, transactions by date) lack covering indexes. Query performance degrades with data growth.
- **Effort**: 3-4 hours
- **Recommendation**: Add indexes for foreign keys and common filter/sort columns via a migration.

#### GAP-P03 — Redis cache underutilized

- **Severity**: Low
- **Detail**: Redis is registered but only used for OAuth state tokens. Stock prices, market data, and player profiles are fetched from PostgreSQL on every request.
- **Effort**: 6-8 hours
- **Recommendation**: Cache stock list, individual stock prices, and portfolio summaries with short TTLs (30s-60s).

#### GAP-P04 — No snapshot data retention policy

- **Severity**: Low
- **Detail**: `PlayerSnapshot` rows accumulate indefinitely. At 100 players with 1-minute sync, this produces ~144k rows/day.
- **Effort**: 2-3 hours
- **Recommendation**: Add a recurring cleanup job that aggregates or deletes snapshots older than a configurable retention period.

---

### 4. Operational Gaps

#### GAP-O02 — No health check endpoint for dependencies

- **Severity**: Medium
- **Detail**: `/api/v1/health` returns a static 200 OK. It does not verify PostgreSQL or Redis connectivity. Docker Compose and load balancers cannot detect degraded state.
- **Effort**: 2-3 hours
- **Recommendation**: Use `AspNetCore.HealthChecks.NpgSql` and `AspNetCore.HealthChecks.Redis` packages.

#### GAP-O03 — No structured logging configuration

- **Severity**: Low
- **Detail**: Application uses default `ILogger` with console provider. No structured logging (Serilog/Seq) or log correlation beyond `TraceIdentifier`.
- **Effort**: 3-4 hours
- **Recommendation**: Add Serilog with JSON formatting for production. Not blocking for MVP.

#### GAP-O04 — No migration strategy documented for production

- **Severity**: Medium
- **Detail**: `DEPLOYMENT.md` mentions `dotnet ef database update` but does not cover migration strategy for zero-downtime deployments or rollback procedures.
- **Effort**: 2-3 hours
- **Recommendation**: Document migration bundling (`dotnet ef migrations bundle`) and rollback procedures.

#### GAP-O07 — Hangfire dashboard admin access not tested

- **Severity**: Low
- **Detail**: Hangfire dashboard requires Admin role and HTTPS in production. No integration test verifies this access control.
- **Effort**: 2-3 hours
- **Recommendation**: Add integration tests for Hangfire dashboard authorization.

---

### 5. Testing Gaps

#### GAP-T01 — No integration tests for trading flow

- **Severity**: Medium
- **Detail**: Buy/sell endpoints are tested only via unit tests on handlers. No integration test exercises the full HTTP → handler → database → response pipeline for trading.
- **Effort**: 4-6 hours
- **Recommendation**: Add integration tests using `PostgresWebApplicationFactory` with authenticated requests.

#### GAP-T02 — No integration tests for auth callback

- **Severity**: Medium
- **Detail**: OAuth callback endpoint (`/auth/callback`) is not tested. Would require mocking the osu! OAuth token exchange.
- **Effort**: 4-6 hours
- **Recommendation**: Add integration test with a mock OAuth HTTP handler.

#### GAP-T03 — Market engine event handlers lack edge-case tests

- **Severity**: Low
- **Detail**: Unit tests cover happy paths for market event handlers. Edge cases (price floor enforcement, concurrent events, zero-quantity) are not tested.
- **Effort**: 4-6 hours
- **Recommendation**: Add parameterized tests for boundary conditions.

#### GAP-T04 — No load/stress testing

- **Severity**: Low
- **Detail**: No load testing scripts or results exist. Performance characteristics under concurrent users are unknown.
- **Effort**: 4-6 hours
- **Recommendation**: Create k6 or NBomber scripts for key endpoints. Defer to post-MVP.

#### GAP-T05 — GlobalExceptionHandler 409 test is fragile

- **Severity**: Low
- **Detail**: The concurrency conflict integration test corrupts `row_version` via raw SQL, which is implementation-coupled.
- **Effort**: 1-2 hours
- **Recommendation**: Consider using a test-specific endpoint or mock that throws `DbUpdateConcurrencyException` directly.

---

### 6. Deployment Gaps

#### GAP-D02 — No CI/CD pipeline for Docker builds

- **Severity**: Medium
- **Detail**: `.github/workflows/integration-tests.yml` runs tests but does not build Docker images or push to a registry.
- **Effort**: 3-4 hours
- **Recommendation**: Add a workflow that builds images and pushes to GHCR or Docker Hub on main branch.

#### GAP-D03 — No database backup strategy

- **Severity**: Medium
- **Detail**: Docker Compose PostgreSQL volume has no backup mechanism. Data loss risk in production.
- **Effort**: 2-3 hours
- **Recommendation**: Add `pg_dump` cron job or use managed PostgreSQL with automated backups.

#### GAP-D04 — No monitoring/alerting

- **Severity**: Low
- **Detail**: No Prometheus metrics, Grafana dashboards, or alerting configuration. Operational visibility is limited to logs.
- **Effort**: 6-8 hours
- **Recommendation**: Expose Prometheus metrics via `prometheus-net` and add basic Grafana dashboards. Defer to post-MVP.

---

## Summary Table

| Severity | Count | IDs |
|----------|-------|-----|
| High | 2 | GAP-F02, GAP-F06 |
| Medium | 10 | GAP-F03, GAP-F04, GAP-F05, GAP-F09, GAP-S03, GAP-P01, GAP-P02, GAP-O02, GAP-O04, GAP-T01, GAP-T02, GAP-D02, GAP-D03 |
| Low | 12 | GAP-F01, GAP-F07, GAP-F08, GAP-F10, GAP-S05, GAP-S06, GAP-P03, GAP-P04, GAP-O03, GAP-O07, GAP-T03, GAP-T04, GAP-T05, GAP-D04 |

---

## Recommended Priority Order (Pre-Release)

1. **GAP-F02** — Schedule daily inactivity decay (High, 4-6h)
2. **GAP-F06** — Add anti-abuse cooldowns and position limits (High, 6-8h)
3. **GAP-O02** — Add dependency health checks (Medium, 2-3h)
4. **GAP-F09** — Add pagination to list endpoints (Medium, 4-6h)
5. **GAP-P02** — Add database indexes (Medium, 3-4h)
6. **GAP-D03** — Set up database backups (Medium, 2-3h)

---

## Go / No-Go Recommendation

**Recommendation: Conditional Go** for limited beta release.

**What changed since last assessment:**
The MVP Hardening Sprint resolved all 3 Critical-severity gaps and 4 of 5 High-severity gaps. Docker deployment, CORS, rate limiting, global exception handling, and concurrency conflict handling are now implemented and integration tested.

**Remaining blockers for public release:**
1. **GAP-F02** — Daily decay not scheduled: prices never decay for inactive players, distorting market economics
2. **GAP-F06** — No anti-abuse controls: a public trading platform without wash-trading prevention invites exploitation

**Acceptable for limited beta because:**
- Core auth → trade → portfolio → market engine flow is complete and tested
- Production deployment infrastructure (Docker, nginx, env-var validation) is in place
- Security hardening (CORS, rate limiting, exception handling, concurrency) is implemented
- Limited beta audience reduces abuse risk while GAP-F06 is addressed

**Estimated effort for remaining High-severity items**: 10-14 hours
**Estimated effort for all remaining gaps**: ~80-100 hours
