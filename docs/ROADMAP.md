# ROADMAP.md

# Vision

Build a fantasy stock market around osu! players using virtual currency and performance-driven market mechanics.

---

# Phase 0 - Foundation

Status:
Current

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

# Phase 2 - Market Intelligence

Priority:
High

Features:

* Price history
* Market trends
* Stock charts
* Historical analytics
* Market event feed

Exit Criteria:

Users understand why prices changed.

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
8. Leaderboards
9. Admin Panel
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

Leaderboard operational.

Milestone 8

Public MVP release.

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
