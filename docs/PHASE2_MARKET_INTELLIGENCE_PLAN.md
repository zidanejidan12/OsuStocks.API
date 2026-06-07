# Phase 2 — Market Intelligence (implementation plan)

## Status (2026-06-07)
- **Batch 1 — DONE & integrated** (read-only): Foundation (`IReadModelCache`/`RedisReadModelCache`, indexes `ix_trade_stock_executed` + `ix_stock_history_created`, migration `AddReadModelIndexes` applied), 2.3 Event Feed (`GET /market/events`, `/market/events/{stockId}`), 2.2 Candles (`GET /market/stocks/{id}/history?range=`), 2.5 Analytics (`GET /market/stocks/{id}/analytics`). Build green; integration suite 61/62 (only the pre-existing concurrency test). Adversarial review caught + fixed a Postgres timezone bug in the OHLC SQL.
- **Batch 1 tests — DONE**: Postgres-backed integration tests for feed/candles/analytics. Caught + fixed real bugs: OHLC bucketing (rewritten to epoch-aligned `to_timestamp(floor(epoch/width)*width)`) and a `SqlQueryRaw<decimal>` scalar failure (volatility moved to C#).
- **Batch 2 — DONE & TESTED**: 2.1 Leaderboards (`/api/v1/leaderboards/{wealth,profit,traders}?period=`), 2.4 Trending (`/api/v1/market/trending`), and the `user_wealth_snapshots` table + daily `wealth-snapshot` Hangfire job (02:30 UTC), migration `AddWealthSnapshots` applied. Profit = net-worth-based. Integration tests added (leaderboards/trending/snapshot-capture). Test factory now overrides `IReadModelCache` with a pass-through (deterministic cached endpoints). Suite 81/82 (only the deferred concurrency test).
- **Batch 3 — READY**: Notifications. **Decisions locked:** audience = **holders-only** (fan out off existing PpIncreased/TopPlayDetected events to users holding the stock); **real-time push OUT of scope** (persist + REST list/unread/mark-read; seam left for SignalR later). Scope: `notifications` table + repo, new `INotificationHandler` fan-out handlers, `GET /api/v1/notifications?unread=&page&pageSize` + `POST /{id}/read` + `POST /read-all`.

> Goal: make the market feel alive and informative. All additions are **read-side** (CQRS queries) plus one small write-side feature (notifications) and a couple of background aggregations. Everything follows the existing vertical-slice + Result + EF-read-repo conventions — no re-architecture.

## Conventions this plan follows (verified in code)
- **Vertical slices**: `src/Application/Features/<Area>/<UseCase>/` → `XQuery` + `XHandler` + `XValidator` + `XResponse` (sealed; MediatR + FluentValidation auto-registered). Scaffold with the **add-slice** skill.
- **Read models** live in `src/Domain/Models/...`; **read repositories** are Domain contracts + Infrastructure impls, **manually registered** in `Infrastructure/DependencyInjection.cs`, all `AsNoTracking()`.
- **Endpoints**: minimal-API groups in `src/Api/Program.cs`; `Result<T>` → HTTP via `ResultHttpMapper`. New read endpoints get `.RequireAuthorization()`.
- **Aggregations**: Hangfire recurring jobs (`BackgroundJobs/*RecurringJob.cs` + `OsuSynchronizationRecurringJobRegistrar.Register()` + Worker).
- **Caching**: `IDistributedCache` (Redis) — pattern from `DistributedOsuTokenManager` (JSON + TTL). No read-model cache helper exists yet → we add one.
- **Migrations**: **ef-migration** skill. Indexes already exist for the hot read paths (see `20260605165937_AddCompositeReadPathIndexes`).

---

## Foundation (do first — shared by 2.1/2.4/2.5)
**F1. Read-model cache helper.** New `IReadModelCache` (Application contract) + `RedisReadModelCache` (Infrastructure) wrapping `IDistributedCache`: `GetOrSetAsync<T>(key, ttl, factory)`. Backs leaderboards/trending so heavy aggregates aren't recomputed per request.

**F2. Trade aggregation index.** Add `ix_trade_stock_executed` on `trades (stock_id, executed_at DESC)` — current indexes are `ix_trade_stock` (stock only) and `ix_trade_user_executed_desc` (user). Trending/analytics group by `stock_id` over time windows and need this. *(ef-migration)*

**F3. Time-window helper.** Small value object `MarketWindow { Day, Week, Month }` → `(from, to)` resolution, reused across profit/traders/trending/analytics.

---

## 2.1 Leaderboards
**Endpoints** (`/api/v1/leaderboards`, auth required):
- `GET /wealth?period=daily|weekly|monthly&page&pageSize`
- `GET /profit?period=…`
- `GET /traders?period=…`

**Area**: new `Features/Leaderboards/{GetWealthLeaderboard,GetProfitLeaderboard,GetTraderLeaderboard}`.
**Repo**: `ILeaderboardReadRepository` → `GetWealthAsync/GetProfitAsync/GetTradersAsync(MarketWindow, skip, take)`; `LeaderboardEntryReadModel(rank, userId, username, avatarUrl, value, …)`.

**Computation**:
- **Wealth** = `wallets.balance + Σ(holdings.quantity × player_stocks.current_price)` per user (join users→wallets, users→portfolios→holdings→player_stocks). Point-in-time.
- **Profit** = net P/L. **Decision needed** (see below): realized-only vs net-worth. Recommended v1: `currentWealth − netDeposits`, where netDeposits = Σ wallet_transactions of type InitialGrant/AdminGrant − AdminDeduction. Captures total gains cleanly.
- **Traders** = activity over the window: `Σ trades.total_amount` (or trade count) where `executed_at ∈ window`, grouped by user.

**Performance**: wealth/profit scan all users → **compute via a recurring job** (F1 cache or a snapshot table) rather than per-request. See L1.

**L1 (recommended). `user_wealth_snapshots` table + daily Hangfire job** — `(user_id, captured_at, wealth, profit)`. Enables *period* wealth rankings and "rising" deltas (which a live join can't do historically). Without it, the wealth leaderboard is "current only" and `period` only meaningfully applies to profit/traders. *(ef-migration + new `LeaderboardSnapshotRecurringJob`, daily)*

---

## 2.2 Enhanced Price History (candles)
**Endpoint**: extend `GET /api/v1/market/stocks/{id}/history?range=1h|24h|7d|30d` (back-compat: no param → current flat list).
**Repo**: `IMarketReadRepository.GetStockCandlesAsync(stockId, HistoryRangeSpec)` → `StockCandleReadModel(bucketStart, open, high, low, close, volume)`.

**Bucketing**: PostgreSQL `date_trunc` over `stock_price_history` (indexed `(stock_id, created_at DESC)`), bucket granularity derived from range (1h→1min, 24h→30min, 7d→6h, 30d→1d). OHLC: open = first `new_price` in bucket, close = last, high = max, low = min; `volume` = Σ `trades.quantity` in the bucket. Use **raw SQL** (`FromSqlInterpolated` / a keyless entity) — EF LINQ can't express `date_trunc`/window OHLC cleanly.

**Decision**: `range` = lookback window (recommended) vs explicit `bucket` size. Note price history is event-driven (sparse) → candles reflect actual changes, gaps are expected.

---

## 2.3 Market Event Feed ("why prices moved")
**Endpoints** (auth):
- `GET /api/v1/market/events?page&pageSize&type=` (global)
- `GET /api/v1/market/events/{stockId}?page&pageSize`

**Key design choice**: build the feed primarily from **`stock_price_history`**, not `market_events`. Reason: `stock_price_history` already has `previous_price` + `new_price` (→ exact `%`) and `reason` covering the *full* set the UI wants — `BuyPressure`, `SellPressure`, `PPGain`, `TopPlay`, `Decay`, `AdminAdjustment`. `market_events` only holds PpIncreased/TopPlayDetected/PlayerInactive and has **no price delta**. So:
- Primary source = `stock_price_history` joined to `tracked_players.username`; `%` = `(new−prev)/prev`; map `reason` → human text ("Heavy buy pressure", "Top play detected", "Inactive — decay", …).
- Enrich with `market_events.payload` (PP values, top-score id) by matching `(stock_id, created_at)` proximity where useful.
- This avoids a schema change. *(Alternative if you prefer richer events: add `market_events.price_change_percent` populated at creation — more work, deferred.)*

**Repo**: `IMarketActivityReadRepository.GetFeedAsync(spec)` → `MarketActivityItemReadModel(stockId, playerName, reason, description, percentChange, newPrice, occurredAt)`. Indexed by `ix_stock_history_stock_created_desc`; global feed needs an index on `stock_price_history (created_at DESC)` → **add `ix_stock_history_created`**. *(ef-migration)*

---

## 2.4 Trending Players
**Endpoint**: `GET /api/v1/market/trending?window=24h&limit=10` → sections: `mostBought`, `mostSold`, `fastestRising`, `fastestFalling`, `highestVolume`.
**Repo**: `ITrendingReadRepository.GetTrendingAsync(window, limit)` → `TrendingReadModel` (lists of `TrendingStockReadModel(stockId, playerName, metricValue, currentPrice)`).

**Computation** (window default 24h; uses F2 index):
- mostBought/mostSold = `Σ trades.quantity` filtered `trade_type` + `executed_at ∈ window`, grouped by stock, top N.
- highestVolume = `Σ trades.total_amount` in window.
- fastestRising/falling = `%` change over window = `(current_price − price_at_window_start)/price_at_window_start`, reusing the 24h-change logic already in `MarketReadRepository`.

**Caching**: F1 with ~60s TTL (trending is fine slightly stale).

---

## 2.5 Stock Analytics (detail page)
**Endpoint**: `GET /api/v1/market/stocks/{id}/analytics`.
**Repo**: `IMarketReadRepository.GetStockAnalyticsAsync(stockId)` → `StockAnalyticsReadModel`.

**Metrics**:
- `volume24h` / `volume7d` = Σ `trades.quantity` (shares) **and** Σ `total_amount` (value) in window — expose both.
- `volatility` = `stddev_samp` of per-step returns from `stock_price_history` over 7d (Postgres `stddev_samp`).
- `ownershipCount` = `COUNT(DISTINCT portfolio_id)` in `holdings` where `stock_id` and `quantity > 0` (uses `ix_holding_stock`).
- `activeTraders` = `COUNT(DISTINCT user_id)` in `trades` for the stock in 24h.
- `marketCap` = `Σ holdings.quantity × player_stocks.current_price` (shares outstanding × price).

Single-stock scoped → cheap; light/no caching.

---

## 2.6 Notification Infrastructure (prepare for alerts)
Scope for now: **persist + expose** user-facing notifications; **real-time push (SignalR/web-push) is deferred** but seamed.

- **Entity/table** `notifications`: `(id, user_id, type, title, body, data jsonb, is_read, created_at)` + index `(user_id, created_at DESC)`, `(user_id, is_read)`. *(ef-migration)*
- **Repo** `INotificationRepository` (write) + `INotificationReadRepository` (read).
- **Fan-out handler**: extend the existing MediatR handlers for `PpIncreasedNotification` / `TopPlayDetectedNotification` (already published during sync) to create `notifications` rows for users **holding that stock** (v1 audience = current holders; a `watchlist` table is a clean follow-on).
- **Endpoints** (auth): `GET /api/v1/notifications?unread=&page&pageSize`, `POST /api/v1/notifications/{id}/read`, `POST /api/v1/notifications/read-all`.
- **Seam for real-time**: the create-notification path is the single choke point where a future SignalR hub / web-push can also fire.

---

## Cross-cutting
- **Tests**: per endpoint, an integration test (Postgres Testcontainer, `PostgresWebApplicationFactory`) + handler unit tests for mapping. Mind the single-SELECT query-count convention (`QueryCountingCommandInterceptor`) for the read paths.
- **Docs**: update `API_SPEC.md` + `FRONTEND_API_CONTRACT.md` with the new routes/shapes.
- **Rate limiting**: read endpoints stay unlimited (consistent with current market reads); consider a light limiter only if abused.

## Suggested sequencing
1. **Foundation** (F1 cache, F2 trade index, F3 window helper).
2. **2.3 Event feed** + **2.2 candles** — high value, reuse existing data, no new heavy compute.
3. **2.5 analytics** + **2.4 trending** — share the trade-window aggregation + F2 index.
4. **2.1 leaderboards** — heaviest; lands on F1 + the L1 snapshot job.
5. **2.6 notifications** — entity + endpoints + event fan-out (real-time deferred).

## Decisions needed before building
1. **Profit definition**: net-worth-based (`wealth − netDeposits`, recommended) vs realized-trades-only.
2. **Wealth leaderboard period**: add the `user_wealth_snapshots` job (true historical/period + "rising") vs current-only for v1.
3. **Volume unit**: shares, credit value, or both (plan assumes both).
4. **History param**: `range` (lookback, recommended) vs explicit `bucket` size.
5. **Event feed source**: derive from `stock_price_history` (recommended, no schema change) vs add `price_change_percent` to `market_events`.
6. **Notifications v1 audience**: holders-only (recommended) vs add a watchlist; confirm real-time is out of scope for now.
