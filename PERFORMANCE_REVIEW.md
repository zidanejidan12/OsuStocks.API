# Performance Review

Date: 2026-06-06
Scope: Re-run static architecture and code-path review of query/read flows and synchronization worker throughput on the current solution. No runtime profiling or EXPLAIN ANALYZE was executed in this review.

Verification snapshot:
- `dotnet restore OsuStocks.sln -v minimal` succeeded.
- `dotnet build OsuStocks.sln --no-restore -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false -v minimal` succeeded.
- `dotnet test tests/OsuStocks.Api.IntegrationTests/OsuStocks.Api.IntegrationTests.csproj --no-build --filter "ProjectedReadRepositoryQueryCountTests" -m:1 -v minimal` succeeded (3/3).

Status legend:
- **Resolved**
- **Partially Resolved**
- **Outstanding**

## 1) N+1 Queries

### Finding 1: Portfolio summary endpoint has N+1 stock/player lookups
- **Resolved**
- Current behavior: handler now uses `IPortfolioReadRepository.GetPortfolioSummaryHoldingsByUserIdAsync(...)` with server-side projection.
- Evidence:
  - `src/Application/Features/Portfolio/GetPortfolioSummary/GetPortfolioSummaryQueryHandler.cs`
  - `src/Infrastructure/Persistence/Repositories/PortfolioReadRepository.cs`

### Finding 2: Holdings endpoint has the same N+1 pattern
- **Resolved**
- Current behavior: handler now uses projected read query `GetHoldingsByUserIdAsync(...)`.
- Evidence:
  - `src/Application/Features/Trading/GetHoldings/GetHoldingsQueryHandler.cs`
  - `src/Infrastructure/Persistence/Repositories/PortfolioReadRepository.cs`

### Finding 3: Trade history endpoint has paged N+1 lookups
- **Resolved**
- Current behavior: handler now uses `ITradeReadRepository.GetTradeHistoryByUserIdAsync(...)` projection with player name included.
- Evidence:
  - `src/Application/Features/Trading/GetTradeHistory/GetTradeHistoryQueryHandler.cs`
  - `src/Infrastructure/Persistence/Repositories/TradeReadRepository.cs`
  - Query-count validation: `ProjectedReadRepositoryQueryCountTests` passed.

## 2) Query Projections

### Gaps

1. `TradeRepository.GetByUserIdAsync` returns full entities.
- **Partially Resolved**
- Notes: this remains true for command/repository path, but read path no longer depends on it for trade history endpoint.
- Evidence:
  - `src/Infrastructure/Persistence/Repositories/TradeRepository.cs`
  - `src/Infrastructure/Persistence/Repositories/TradeReadRepository.cs`

2. `HoldingRepository.GetByPortfolioIdAsync` returns full entities.
- **Partially Resolved**
- Notes: this remains true for command/repository path, but read endpoints now use projected read repository.
- Evidence:
  - `src/Infrastructure/Persistence/Repositories/HoldingRepository.cs`
  - `src/Infrastructure/Persistence/Repositories/PortfolioReadRepository.cs`

### Existing positive pattern
- **Resolved** - projected read-model patterns are now implemented broadly for portfolio/holdings/trade history (in addition to wallet transactions).

## 3) Indexes

### Missing composite indexes with strong ROI
- **Resolved**
- Status: composite indexes were added via migration for all listed filter+sort paths.
- Evidence:
  - `src/Infrastructure/Persistence/Migrations/20260605165937_AddCompositeReadPathIndexes.cs`
  - Added: `ix_trade_user_executed_desc`, `ix_wallet_transactions_wallet_created_desc`, `ix_stock_history_stock_created_desc`, `ix_snapshot_player_captured_desc`, `ix_tracked_players_active_tier_username`, `ix_market_events_stock_created_desc`.

## 4) Synchronization Worker Throughput

### Throughput bottlenecks

1. Full synchronization loop is sequential by tracked player.
- **Outstanding**
- Evidence: `foreach` loop remains in `PlayerSynchronizationService`.

2. Each player synchronization does two outbound osu API calls (`user` + `top score`).
- **Outstanding**
- Evidence: `GetUserInternalAsync` still calls `SendAsync` for user and `GetTopScoreAsync`.

3. Notifications are published inside the per-player loop and awaited serially.
- **Outstanding**
- Evidence: publish calls remain inside nested loop in `PlayerSynchronizationService`.

4. Market event processing commits per event-handler execution.
- **Outstanding**
- Evidence: `MarketEventProcessingService` still calls `dbContext.SaveChangesAsync(...)` in `ApplyInternalAsync(...)`.

5. Recurring jobs had no overlap guard attribute.
- **Resolved**
- Evidence: `[DisableConcurrentExecution(timeoutInSeconds: 30)]` is present on tier job methods in `OsuSynchronizationRecurringJob`.

### Recommended throughput fixes (status)
1. Add overlap protection for recurring sync jobs.
- **Resolved**

2. Introduce bounded parallelism for external API fetch stage.
- **Outstanding**

3. Batch market price updates and reduce per-event commits.
- **Outstanding**

4. Cache market coefficients/settings briefly per synchronization batch.
- **Outstanding**
- Evidence: `MarketCoefficientsProvider.GetCurrentAsync(...)` still reads settings each call; no short-lived batch cache layer added.

5. Add metrics: players/sec, API latency, events/sec, DB save duration, tier lag.
- **Partially Resolved**
- Evidence: recurring-job metrics counters/histogram were added (`OsuSynchronizationRecurringJobMetrics`), but detailed per-stage latency and DB-save duration metrics are not yet fully covered.

## ROI-Ranked Fixes (current status)

| Rank | Fix | Status |
|---|---|---|
| 1 | Replace N+1 read handlers with projected join queries (portfolio, holdings, trade history) | **Resolved** |
| 2 | Add composite indexes for filter+sort paths | **Resolved** |
| 3 | Add overlap guard to recurring sync jobs | **Resolved** |
| 4 | Reduce synchronization write amplification (batch market processing, fewer commits) | **Outstanding** |
| 5 | Add bounded parallelism for osu API fetch stage with throttling | **Outstanding** |
| 6 | Introduce short-lived settings/coefficient cache in sync pipeline | **Outstanding** |

## Suggested Execution Order (updated)
1. Optimize synchronization write amplification (batch market processing and fewer `SaveChangesAsync` calls).
2. Add bounded parallel fetch with explicit rate-limit controls.
3. Add short-lived settings/coefficient cache in sync pipeline.
4. Expand sync observability with per-stage latency and DB-write metrics.
