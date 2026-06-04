# USE_CASES.md

# Overview

osu! Stocks is a fantasy stock market game where users trade virtual shares representing tracked osu! players.

The system uses osu! player performance and community trading activity to influence stock prices.

---

# User Use Cases

## UC-001 Login with osu!

Actor:
User

Preconditions:

* User owns an osu! account

Main Flow:

1. User clicks Login
2. User is redirected to osu! OAuth
3. User authorizes access
4. System creates account if first login
5. System grants starting credits
6. User enters dashboard

Postconditions:

* Authenticated session exists

---

## UC-002 View Market

Actor:
Authenticated User

Main Flow:

1. User opens Market page
2. System loads active stocks
3. System displays:

   * Current Price
   * 24h Change
   * Volume
   * Player Statistics

Success:
Market information displayed

---

## UC-003 View Stock Details

Actor:
Authenticated User

Main Flow:

1. User selects a stock
2. System displays:

   * Player information
   * Price history
   * Market statistics
   * Recent events

Success:
Stock details displayed

---

## UC-004 Buy Stock

Actor:
Authenticated User

Preconditions:

* Wallet balance available
* Stock active

Main Flow:

1. User enters quantity
2. System calculates cost
3. System validates balance
4. Trade executes
5. Holdings updated
6. Wallet updated
7. Market event created

Failure:

* Insufficient balance
* Stock disabled
* Market maintenance mode

---

## UC-005 Sell Stock

Actor:
Authenticated User

Preconditions:

* Holdings available

Main Flow:

1. User selects quantity
2. System validates ownership
3. Trade executes
4. Holdings updated
5. Wallet credited

Failure:

* Insufficient holdings
* Stock disabled

---

## UC-006 View Portfolio

Actor:
Authenticated User

Main Flow:

1. User opens Portfolio
2. System calculates:

   * Holdings
   * Current value
   * Unrealized profit/loss
   * Historical performance

Success:
Portfolio displayed

---

## UC-007 View Leaderboards

Actor:
Authenticated User

Main Flow:

1. User opens leaderboard
2. System displays:

   * Richest Users
   * Highest ROI
   * Best Performing Stocks

Success:
Leaderboard displayed

---

# Admin Use Cases

## UC-101 Add Tracked Player

Actor:
Administrator

Main Flow:

1. Search osu! player
2. Select player
3. Configure initial stock settings
4. Enable tracking

Postconditions:

* Stock created
* Sync scheduled

---

## UC-102 Disable Tracked Player

Actor:
Administrator

Main Flow:

1. Select tracked player
2. Disable trading

Postconditions:

* Buying disabled
* Selling allowed

---

## UC-103 Configure Market Settings

Actor:
Administrator

Main Flow:

1. Open Market Settings
2. Modify coefficients
3. Save configuration

Examples:

* PP multiplier
* Trading multiplier
* Decay multiplier

Postconditions:

* New values applied

---

# System Use Cases

## UC-201 Synchronize osu! Data

Actor:
System

Trigger:
Scheduled job

Main Flow:

1. Load tracked players
2. Fetch latest data
3. Compare snapshots
4. Detect changes
5. Emit events

---

## UC-202 Process Market Events

Actor:
System

Trigger:
Domain Event

Main Flow:

1. Receive event
2. Calculate impact
3. Update stock price
4. Persist history
5. Publish price change

---

## UC-203 Daily Reward Distribution

Actor:
System

Trigger:
Daily schedule

Main Flow:

1. Identify eligible users
2. Grant reward
3. Create ledger entries
4. Notify users
