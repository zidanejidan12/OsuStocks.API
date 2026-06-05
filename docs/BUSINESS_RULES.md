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

If player has no positive performance activity:

Apply decay.

Default:

-0.5% daily

Admin configurable.

---

## BR-016 Inactive Player

Definition:

No PP gain for 14 consecutive days.

Effect:

Additional decay applied.

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

## BR-041 Self-Trading Prevention

Users may not execute invalid trades designed solely to manipulate volume.

Suspicious patterns are logged.

---

## BR-042 Market Manipulation Monitoring

Repeated circular trading patterns should be flagged.

Administrator review required.

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

