# BUSINESS_RULES.md

# Trading Rules

## BR-001 Virtual Currency Only

The platform uses virtual credits.

No real-money deposits.

No real-money withdrawals.

No conversion to real-world value.

---

## BR-002 Starting Balance

New users receive:

100,000 Credits

Upon first login.

Granted once.

---

## BR-003 Fractional Shares

Fractional shares are not allowed.

All trades use whole share quantities.

---

## BR-004 Buy Validation

A purchase may only occur if:

* User authenticated
* Stock active
* Quantity > 0
* Wallet balance sufficient

---

## BR-005 Sell Validation

A sale may only occur if:

* User owns shares
* Quantity <= holdings

---

# Market Rules

## BR-010 Price Floor

Stock price may never fall below:

1 Credit

---

## BR-011 Price Ceiling

No hard ceiling exists.

---

## BR-012 New Top Play Impact

Trigger:

Player achieves new top play.

Impact:

+1% to +10%

Based on PP gain.

Admin configurable.

---

## BR-013 PP Gain Impact

Trigger:

Player gains PP.

Impact:

Positive increase proportional to PP gained.

---

## BR-014 Trading Impact

Every completed trade affects stock demand score.

Demand score influences future price movement.

---

## BR-015 Daily Decay

If a tracked player's latest snapshot is older than the inactivity threshold:

Apply decay via `PlayerInactive` event.

Default threshold: 7 days (configurable via `MarketEngine:InactivityThresholdDays`).

Decay impact: -0.5% per evaluation (configurable via `MarketEngine:InactivityDecayImpact`).

Evaluated once daily by the `inactivity-decay` recurring job (runs at 03:00 UTC).

The existing `PlayerInactiveEventHandler` applies the price change through the market engine.

---

## BR-016 Inactive Player

Definition:

No snapshot activity for the configured inactivity threshold (default: 7 days).

Effect:

`PlayerInactive` event published, triggering decay through the market engine's coefficient-based pricing.

---

# Player Tracking Rules

## BR-020 Tracked Players Only

Only administrator-approved players may become stocks.

---

## BR-021 Disabled Player

Disabled players:

Cannot be purchased.

Existing holders may sell.

---

## BR-022 Delisted Player

Administrator may delist player.

Effects:

* New purchases disabled
* Existing sales allowed
* Historical data retained

---

# Wallet Rules

## BR-030 Ledger Integrity

Wallet transactions are immutable.

Updates never modify historical records.

---

## BR-031 Negative Balance

Wallet balances may never become negative.

---

# Anti-Abuse Rules

## BR-040 Single osu! Identity

One osu! account equals one game account.

---

## BR-041 Self-Trading Prevention / Trade Cooldown

A per-stock trade cooldown prevents rapid buy-sell cycling on the same stock.

Default: 30 seconds (configurable via `AntiAbuse:TradeCooldownSeconds`).

Applies to both buy and sell operations per user per stock.

Violations are logged as structured warnings and return `TRADE_COOLDOWN` error.

---

## BR-042 Market Manipulation Monitoring

### Position Limit

Users may not accumulate more than a configurable percentage of a stock's total supply.

Default: 25% (configurable via `AntiAbuse:MaxOwnershipPercentage`).

Exceeding the limit returns `POSITION_LIMIT_EXCEEDED` error.

Position limit is not enforced when total supply is zero (first buyer).

### Rapid Trading Detection

When a user executes more trades than a configurable threshold within a time window, a structured warning is logged for administrator review.

Default: 10 trades within 300 seconds (configurable via `AntiAbuse:RapidTradeThreshold` and `AntiAbuse:RapidTradeWindowSeconds`).

Rapid trading detection is non-blocking (audit only).

### Audit Logging

All violations and suspicious patterns produce structured log entries with:

- `UserId`, `StockId`
- Violation type and parameters
- Timestamps and thresholds

---

# Leaderboard Rules

## BR-045 Wealth Leaderboard

Wealth = wallet balance + market value of all positive holdings (`quantity * stock.currentPrice`).

Users are ranked by current wealth, ties broken by username.

Period-over-period change = current wealth minus the most recent wealth snapshot captured at or before the period start (`null` when no snapshot exists for that user).

---

## BR-046 Profit Leaderboard

Profit (net worth) = Wealth − NetDeposits.

NetDeposits = SUM(deposit-type transaction amounts) − SUM(AdminDeduction amounts), where:

* Deposit types = `InitialGrant` + `AdminGrant` + `DailyReward`
* Deductions = `AdminDeduction`

All wallet-transaction amounts are stored positive; the deduction sum is subtracted.

Users are ranked by profit, ties broken by username. Period-over-period change uses the snapshot `Profit` value (null when no snapshot exists).

---

## BR-047 Trader Leaderboard

Traders are ranked by traded credit volume within the period — `SUM(trade.totalAmount)` for trades executed at or after the period start.

No period-over-period change is reported (the value already represents in-window volume).

---

## BR-048 Wealth Snapshot Capture

A daily Hangfire job (`wealth-snapshot`, runs at 02:30 UTC) captures one `WealthSnapshot` per user.

Each snapshot stores `Wealth`, `NetDeposits`, and `Profit` at capture time.

Snapshots are the historical baseline that powers the period-over-period change on the wealth and profit leaderboards. Redis is not the source of truth; snapshots persist in PostgreSQL (`user_wealth_snapshots`).

---

# Notification Rules

## BR-049 Holder Fan-Out Notifications

When a tracked player's stock experiences a market-relevant event, an in-app notification is created for every user that currently holds shares of that stock (holders-only fan-out).

Triggering domain events:

* `PpIncreased` → "{player} gained pp" notification
* `TopPlayDetected` → "{player} set a new top play" notification

Behavior:

* Fan-out is skipped when the player has no stock or the stock has no holders.
* Notifications are created in-process by additional MediatR notification handlers (not a new broker).
* Users read notifications via `/notifications` (with optional `unread` filter) and mark them read via `/notifications/{id}/read` or `/notifications/read-all`.

Out of scope:

* Real-time push (WebSocket / SSE / web-push) delivery — notifications are persisted and polled, not pushed.

---

# Investor Level Rules

## BR-049a Investor XP from Trading

Each user has an `InvestorProfile` tracking lifetime XP and a derived level (Phase 3).

* XP is earned from trading volume: `floor(UnitPrice × Quantity)` — i.e. 1 XP per whole credit of
  gross traded value — on **both** buy and sell executions.
* XP is awarded best-effort **after** the trade commits, by a MediatR handler on the
  `BuyOrderExecuted` / `SellOrderExecuted` events (the same post-commit pattern as holder fan-out
  notifications). It is never deducted; `TotalXp` only increases.
* The profile is created lazily on the user's first XP-earning trade. Users who have never traded
  are treated as level 1 with 0 XP.

## BR-049b Level Curve (osu!-style, soft-capped at 100)

The cumulative XP required to reach level `L` follows the osu! score-to-level formula:

* For `1 ≤ L ≤ 100`: `5000/3 × (4L³ − 3L² − L) + 1.25 × 1.8^(L−60)` (level 1 floor = 0).
* For `L ≥ 100`: `floor(100) + 100,000,000,000 × (L − 100)`.

Consequences:

* Every level requires strictly more XP than the previous one.
* Level 100 is a soft cap: each level beyond it costs a flat 100 billion XP, so `100 → 101` is a
  very large jump relative to earlier levels.

Levels are **cosmetic**: they grant titles only and do not alter any trading, wallet, or market
rule. Title bands: 1–9 Novice Investor, 10–24 Retail Trader, 25–49 Active Trader,
50–74 Seasoned Investor, 75–99 Blue-Chip Trader, 100+ Market Legend.

## BR-049c Level-Up Notification

When an XP award advances a user's level, an in-app notification of type `InvestorLevelUp` is
created (data payload `{"level","title"}`), reusing the existing notification infrastructure.

---

# Synchronization Rules

## BR-050 Polling Strategy

Tier 1:
1 minute

Tier 2:
5 minutes

Tier 3:
15 minutes

---

## BR-051 Snapshot Comparison

Market changes must be derived from snapshot comparisons.

Direct user-triggered synchronization prohibited.

---

# Administrative Rules

## BR-061 Coefficient Management

Administrators may adjust:

* Trading coefficient
* PP coefficient
* Decay coefficient

Changes affect future calculations only.

---

# Maintenance Rules

## BR-060 Market Maintenance

Status:
Implemented (Phase 1.5)

Administrator may enable maintenance mode via `/admin/market-settings` (`isMaintenanceMode` flag).

Effects:

* Trading disabled — buy and sell handlers return a `CONFLICT` error ("Market is in maintenance mode.") while maintenance mode is on.
* Viewing remains available (market, portfolio, wallet, and leaderboard reads are unaffected).

