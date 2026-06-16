# OsuStocks — Market Ruleset

*A plain-language guide to how stock prices are set and what moves the graph.*

---

## 1. What is a "stock"?

Every tracked **osu! player** has one tradeable stock. Its **price** is a live number that
rises and falls based on how that player is performing in the game — think of it as the
player's "market value." Users spend in-game currency to **buy** and **sell** these stocks
and build a portfolio.

The price chart you see is simply the history of that number over time.

---

## 2. The master rule

Every price move works the same way:

```
new price = current price × (1 + impact)
```

…where **impact** is a percentage produced by whatever event just happened (a performance
gain, a rank change, a trade, etc.). Impacts can be **positive** (price up) or **negative**
(price down).

Two guardrails always apply:

- **Price floor:** a stock can never fall below **1** (it can't hit zero).
- **Caps:** every event type has a maximum move so a single event can't spike or crash a stock.

---

## 3. The opening price (when a player is first listed)

A new stock doesn't start at a flat 100 — it opens at a value based on the player's **global
rank**, so stronger players list higher:

```
opening price = 1000 × (global rank ^ −0.37)     (minimum 1)
```

| Global rank | Opening price |
|---|---|
| #1 | ~1000 |
| #50 | ~235 |
| #500 | ~100 |
| #5,000 | ~43 |
| Unranked | 100 (neutral) |

---

## 4. What moves the graph

There are **five** drivers. Each produces an *impact %* that feeds the master rule above.

### 4.1 Rank change — the main day-to-day mover ⬆️⬇️
Global rank moves constantly and in **both directions** (it's a ladder — when others climb,
you slip). This is the primary source of daily volatility.

```
impact = 0.5 × (rank change ÷ old rank)      capped at ±5%
```
- Player **climbs** the ladder → price **up**. Player **slips** → price **down**.
- Scaled by the *relative* move, so a 10% rank jump matters equally whether the player is
  ranked #50 or #5,000.
- **Small wobbles (< 0.2%) are ignored** to avoid noise.

*Example: rank 1,000 → 950 is a 5% improvement → +2.5% price. Rank 1,000 → 1,100 → −5% (capped).*

### 4.2 Performance points (pp) change ⬆️⬇️
When a player gains pp, their stock rises; if pp is lost (a score gets overwritten/recalculated),
it falls.

```
impact = pp gained or lost × 0.02%      capped at ±10%
```
*Example: +200 pp → +4% price.*

### 4.3 New top play ⬆️
When a player sets a notable new top score, the stock gets a bump **scaled to how big that play
is relative to the player's total pp** — a breakout play means more for a smaller player than for
an elite one.

```
impact = 0.6 × (play pp ÷ player's total pp)      capped between 0.5% and 10%
```
*Example: a 700 pp play by a 5,000 pp player → +8.4%. The same play by a 35,000 pp player → ~+1.2%.*

### 4.4 Trading (buy/sell pressure) ⬆️⬇️
The market reacts to its own users — demand pushes a price up, selling pushes it down.

```
buy  → +0.25% per share
sell → −0.25% per share
```

### 4.5 Inactivity decay ⬇️
If a tracked player stops making progress (no pp gains for about a week), their stock slowly
bleeds value while they remain inactive.

```
impact = −0.5% per decay event
```

---

## 5. Coefficients at a glance

| Driver | Formula | Cap |
|---|---|---|
| Opening price | `1000 × rank^(−0.37)` | floor 1 |
| Rank change | `0.5 × Δrank/rank` (min move 0.2%) | ±5% |
| pp change | `pp × 0.0002` | ±10% |
| New top play | `0.6 × playPp/playerPp` | 0.5%–10% |
| Buy / Sell | `±0.25% × shares` | — |
| Inactivity | `−0.5%` per event | — |
| Price floor | — | never below 1 |

---

## 6. Admin controls

Operators can tune the market live without redeploying:

- **pp multiplier**, **trade multiplier**, **decay multiplier** — globally scale the strength of
  each group of drivers (default ×1.0). Turn volatility up or down on demand.
- **Maintenance mode** — temporarily pause all buying and selling.

---

## 7. How often the graph updates

Prices recompute as the worker re-checks each player against the live osu! API:

- **Top players:** as often as **every minute** (near real-time).
- **Mid-tier:** every few to ~15 minutes.
- **The long tail:** about **hourly**.

This keeps the most-watched stocks lively while staying within osu!'s API rate limits.

---

## 8. Data & integrity

- **Source of truth:** PostgreSQL. Player snapshots, every price change, and every market event
  are recorded.
- **Retention:** raw player snapshots are pruned after 14 days; price-history and market-event
  records after 90 days — enough to power charts and feeds while keeping storage bounded.
- **No price goes to zero**, and every single-event move is capped, so the market stays stable.

---

## 9. A worked example — one player's day

> Player "X" is ranked **#1,200** with **9,000 pp**. Their stock opens around **73**.
>
> - They climb to **#1,140** (a 5% rank gain) → **+2.5%** → ~74.8
> - They set a new **750 pp** top play → `0.6 × 750/9,000 = 5%` → **+5%** → ~78.6
> - The total pp gain (+750) also registers → capped contribution → price ticks up further.
> - Meanwhile other players pass a rival "Y", whose rank slips 3% → **−1.5%** on Y's stock.
>
> Traders who bought X early are now up; Y's holders are down. **That two-way movement is what
> makes the market worth trading.**

---

*This document reflects the live pricing engine. All coefficients are configuration values and
can be adjusted as the game is balanced.*
