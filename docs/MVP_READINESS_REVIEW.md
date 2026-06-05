# MVP Readiness Review

Date: 2026-06-06
Reviewer: Principal .NET Architecture Review (Re-run)

Scope reviewed:
- docs/ARCHITECTURE.md
- docs/USE_CASES.md
- docs/BUSINESS_RULES.md
- docs/DOMAIN_MODEL.md
- docs/DATABASE.md
- docs/API_SPEC.md
- docs/CODING_STANDARDS.md
- docs/ROADMAP.md
- Entire src/ and tests/ solution

Verification snapshot:
- `dotnet restore OsuStocks.sln -v minimal` succeeded.
- `dotnet build OsuStocks.sln --no-restore -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false -v minimal` succeeded.
- `dotnet test OsuStocks.sln --no-build -m:1 -v minimal` failed in this environment due test-host constraints (Windows EventLog write access denied for some API integration tests, and Docker/Testcontainers unavailable for PostgreSQL-backed tests).

Status legend:
- **Resolved**
- **Partially Resolved**
- **Outstanding**

## 1. Architectural violations

1. **Outstanding** - Endpoint ownership does not follow the documented vertical-slice standard.
   - Evidence: `docs/CODING_STANDARDS.md` states each slice should include an endpoint; endpoints remain centralized in `src/Api/Program.cs`.

2. **Outstanding** - Business rules are concentrated in application handlers instead of domain entities/aggregates.
   - Evidence: mutation and invariant logic remains in handlers (for example `BuyStockCommandHandler`, `SellStockCommandHandler`); entities such as `Wallet`, `Holding`, and `PlayerStock` are still state-focused models.

3. **Outstanding** - Market model fields `DemandScore` and `PerformanceScore` are not used to drive pricing.
   - Evidence: fields exist and are initialized (for example in `AddTrackedPlayerCommandHandler`) but are not updated/consumed by pricing flow.

4. **Outstanding** - API contract drift for `GET /portfolio/holdings`.
   - Evidence: `docs/API_SPEC.md` defines array response; implementation returns envelope `{ items: [...] }` in `src/Api/Program.cs`.

## 2. Missing MVP functionality

1. **Outstanding** - Leaderboard endpoints are not implemented.
   - Evidence: API spec includes `/leaderboards/*`; no `/api/v1/leaderboards` routes in API.

2. **Outstanding** - Market maintenance mode is missing.
   - Evidence: BR-060 and `409 MarketMaintenance` contract exist in docs; no maintenance-mode guard/state in trading handlers.

3. **Outstanding** - Anti-abuse controls are missing.
   - Evidence: BR-041/BR-042 documented; no detection/monitoring implementation found in `src/`.

4. **Partially Resolved** - Daily decay behavior is partially implemented.
   - Evidence: inactivity event detection exists (`SnapshotComparisonService`), but logic is tied to 14-day inactivity threshold and does not implement explicit daily decay scheduling semantics from BR-015.

## 3. Security concerns

1. **Resolved** - Hangfire dashboard is protected.
   - Evidence: dashboard mapping now requires authorization and admin role; dashboard auth filter enforces authenticated admin and HTTPS outside development (`src/Api/Program.cs`, `src/Api/Security/HangfireDashboardAuthorizationFilter.cs`).

2. **Resolved** - Swagger is no longer always exposed.
   - Evidence: Swagger gated by development environment or `Security:EnableSwagger` flag (`src/Api/Program.cs`).

3. **Resolved** - JWT bearer metadata HTTPS check is environment-aware.
   - Evidence: `RequireHttpsMetadata = !builder.Environment.IsDevelopment()` (`src/Api/Program.cs`).

4. **Partially Resolved** - Placeholder signing key risk is mitigated but default placeholder remains committed.
   - Evidence: production startup now validates required env vars and rejects placeholder secret values, but `src/Api/appsettings.json` still contains placeholder key string.

5. **Outstanding** - OAuth `returnUrl` validation remains minimal.
   - Evidence: validator only enforces max length; no allow-list/domain validation (`GetOsuLoginUrlQueryValidator`, callback flow propagation).

## 4. Performance concerns

1. **Resolved** - N+1 query patterns in portfolio/holdings/trade history have been addressed.
   - Evidence: handlers now use projected read repositories; query-count tests exist for single-select behavior (`ProjectedReadModelsQueryCountTests`, `ProjectedReadRepositoryQueryCountTests`).

2. **Outstanding** - Synchronization path is still sequential per player.
   - Evidence: `foreach` loop in `PlayerSynchronizationService` remains sequential with multiple outbound calls per player.

3. **Outstanding** - Search filter remains non-sargable.
   - Evidence: `ToLower().Contains(...)` still used in `MarketReadRepository`.

4. **Outstanding** - Redis is not used for market-read caching.
   - Evidence: distributed cache usage remains focused on token manager; market read path is uncached.

5. **Partially Resolved** - Optimistic concurrency controls were added but are not complete end-to-end.
   - Evidence: row-version/concurrency tokens and migration added for wallet/holding/player stock, with repository tests; coverage and conflict handling are still partial across all mutable aggregates.

## 5. Maintainability concerns

1. **Outstanding** - API composition remains centralized in a large `Program.cs`.

2. **Outstanding** - Economic constants remain hardcoded in handlers.
   - Evidence: `StartingCredits` in callback handler and initial stock price constant in tracked-player flow.

3. **Partially Resolved** - Integration test realism improved but still mixed.
   - Evidence: PostgreSQL Testcontainers factory exists; separate custom in-memory/no-op integration factory still present.

4. **Outstanding** - Authorization behavior is still under-tested for non-admin users.
   - Evidence: test auth handler injects Admin role; no non-admin matrix coverage found.

5. **Outstanding** - Documentation drift remains.
   - Evidence: `README.md` still lists `/market/*` and `/wallet*` as not fully implemented despite route groups present in API.

## 6. Technical debt

1. **Outstanding** - Critical invariants still rely primarily on application logic (limited DB check constraints).

2. **Outstanding** - Immutability is still explicitly enforced only for `wallet_transactions`.

3. **Partially Resolved** - Cross-document scope inconsistency narrowed but not fully unified for MVP language.
   - Evidence: leaderboard appears in roadmap milestones/development order, but MVP phase scope language and other docs can still be interpreted inconsistently.

4. **Partially Resolved** - Runtime/tooling baseline improved but full test execution is still environment-sensitive.
   - Evidence: .NET 9 runtime is available and build succeeds; full integration execution still blocked in this environment by EventLog permission and Docker availability.

## 7. Go / No-Go recommendation

Recommendation: **No-Go** for public MVP release.

Rationale:
- Core MVP/business-rule gaps remain unresolved (leaderboards, maintenance mode, anti-abuse).
- Multiple architecture and maintainability gaps remain open.
- Security posture improved materially, but open redirect hardening and deployment-default cleanup are not fully complete.

Minimum gates to reach Go:
1. Implement and test `/leaderboards/*` or formally remove/defer from MVP contracts in all docs.
2. Implement market maintenance mode guard and anti-abuse detection/monitoring baseline.
3. Resolve API contract drift (`/portfolio/holdings`) and documentation drift (`README`, scope docs).
4. Address remaining performance debt (sequential sync and non-sargable market search).
5. Expand concurrency/conflict handling coverage across mutable aggregates.
6. Run full integration suite in a CI/local environment with Docker and required host permissions, and publish green evidence.
