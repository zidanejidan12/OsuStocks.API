# ROADMAP.md

# Vision

Build a fantasy stock market around osu! players using virtual currency and performance-driven market mechanics.

---

# Phase 0 - Foundation

Status:
Done

Goals:

* Architecture established
* Documentation completed
* Solution skeleton generated

Deliverables:

* Architecture documents
* Clean Architecture solution
* CI/CD foundation
* Docker environment

Exit Criteria:

Project builds successfully.

---

# Phase 1 - Core Platform (MVP)

Priority:
Critical

Status:
Done

Modules:

* Authentication
* Player Registry
* Wallet
* Market
* Trading
* Portfolio

Features:

* Login with osu!
* Track players
* Buy stock
* Sell stock
* View portfolio
* View market

Exit Criteria:

User can:

1. Login
2. View stocks
3. Buy shares
4. Sell shares
5. Track portfolio

Target:

Playable MVP

---

# Phase 1.5 - Post-MVP Deferred Features

Priority:
High

Status:
Done

Features:

* Leaderboards (wealth, profit, traders) — DONE (`/leaderboards/{wealth,profit,traders}?period=`)
* Market maintenance mode — DONE (admin `IsMaintenanceMode` toggle via `/admin/market-settings`)

Exit Criteria:

Deferred features are implemented and released after MVP stabilization.

---

# Phase 2 - Market Intelligence

Priority:
High

Status:
Done

Features:

* Price history — DONE (`/market/stocks/{id}/history`)
* Market trends / trending — DONE (`/market/trending?window=&limit=`: most bought/sold, fastest rising/falling, highest volume)
* Stock charts (OHLC candles) — DONE (`/market/stocks/{id}/history?range=` returns open/high/low/close/volume buckets)
* Historical analytics — DONE (`/market/stocks/{id}/analytics`: 24h/7d volume, 7d volatility, ownership, active traders, market cap)
* Market event feed — DONE (`/market/events`, `/market/events/{stockId}`)
* Leaderboards (wealth, profit, traders) — DONE
* Daily wealth-snapshot job — DONE (`wealth-snapshot` Hangfire job, daily 02:30 UTC, backs leaderboard period-over-period change)
* Notifications — DONE (holder fan-out on PpIncreased/TopPlayDetected; `/notifications`, `/notifications/{id}/read`, `/notifications/read-all`)
* Player + user avatars and country codes — DONE (avatarUrl / countryCode on market, leaderboard, feed, and /me payloads)

Exit Criteria:

Users understand why prices changed. — MET

---

# Phase 3 - Economy Expansion

Priority:
High

Features:

* Daily rewards
* Login bonuses
* Achievements
* Investor levels
* Missions

Exit Criteria:

Daily retention systems operational.

---

# Phase 4 - Competition Systems

Priority:
Medium

Features:

* Seasonal rankings
* Investor tournaments
* Monthly resets
* Special events

Exit Criteria:

Competitive gameplay introduced.

---

# Phase 5 - Community Features

Priority:
Medium

Features:

* User profiles
* Public portfolios
* Follow investors
* Activity feed

Exit Criteria:

Community engagement systems available.

---

# Phase 6 - Advanced Market Simulation

Priority:
Low

Features:

* Market sentiment score
* Dynamic volatility
* Performance multipliers
* Special stock events

Exit Criteria:

Market behavior becomes more sophisticated.

---

# Phase 7 - Scale & Optimization

Priority:
Conditional

Triggered When:

* 5000+ concurrent users
* Performance bottlenecks observed

Work:

* Worker separation
* Read optimizations
* Caching improvements
* Database tuning

Exit Criteria:

System scales without architectural redesign.

---

# Development Order

1. Authentication
2. Player Registry
3. Osu Integration
4. Wallet
5. Trading
6. Portfolio
7. Market Engine
8. Admin Panel
9. Leaderboards (Phase 1.5)
10. Analytics

---

# Technical Milestones

Milestone 1

Admin can add tracked player.

Milestone 2

Worker synchronizes osu! data.

Milestone 3

Stock automatically created.

Milestone 4

User buys stock.

Milestone 5

Portfolio updates correctly.

Milestone 6

Top play changes stock price.

Milestone 7

Public MVP release.

Milestone 8

Leaderboards and maintenance mode released (Phase 1.5).

---

# Success Metrics

Technical:

* P95 API Response < 1000ms
* Availability > 99%
* No critical data loss

Business:

* Active traders
* Daily active users
* Average session length
* Retention rate

Primary Goal:

Deliver a stable, fun, and maintainable fantasy trading experience around the osu! ecosystem.

