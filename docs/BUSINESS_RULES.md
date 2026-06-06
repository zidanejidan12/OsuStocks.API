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

# Postponed Rules (Phase 1.5)

## BR-060 Market Maintenance

Status:
Postponed

Administrator may enable maintenance mode.

Effects:

* Trading disabled
* Viewing remains available

