# osu! Stocks - Architecture Decision Record (ADR)

## Project Overview

osu! Stocks is a fantasy stock market web application where users invest virtual in-game currency into osu! players.

Each tracked osu! player becomes a tradeable stock.

Stock prices are influenced by:

* Community buying activity
* Community selling activity
* New top plays
* PP gains
* Ranking improvements
* Inactivity decay

No real money is involved.

No cash-out functionality exists.

This is a game platform, not a financial platform.

---

# Team Structure

Current Team Size:

* 1 Developer

All architectural decisions must optimize for:

* Development speed
* Simplicity
* Maintainability
* Long-term extensibility

---

# Architecture Decision

## Chosen Architecture

Modular Monolith

Clean Architecture

Domain Driven Design (DDD)

Lightweight CQRS

Repository Pattern

Dependency Injection

Domain Events

---

## Explicit Non-Goals

The following are intentionally excluded:

* Microservices
* Kubernetes
* Kafka
* Event Sourcing
* Service Mesh
* Distributed Transactions
* Multi-Database Architecture
* Event-Driven Infrastructure

These technologies may be revisited after significant growth.

---

# Technology Stack

## Frontend

Framework:

* Next.js

Language:

* TypeScript

UI:

* TailwindCSS

Data Fetching:

* TanStack Query

Charts:

* Recharts

---

## Backend

Framework:

* ASP.NET Core 9

Language:

* C#

Architecture:

* Clean Architecture
* DDD
* CQRS

Validation:

* FluentValidation

Mediator:

* MediatR

Mapping:

* Mapster

---

## Data

Database:

* PostgreSQL

ORM:

* Entity Framework Core

Cache:

* Redis

---

## Background Jobs

Framework:

* Hangfire

Responsibilities:

* osu! Synchronization (tiered: `osu-sync-tier1/2/3`)
* Top Play Detection
* Reward Distribution
* Market Maintenance
* Inactivity Decay (`inactivity-decay`, daily 03:00 UTC)
* Daily Wealth Snapshot (`wealth-snapshot`, daily 02:30 UTC)
* Daily Jobs

Recurring jobs are registered in `OsuSynchronizationRecurringJobRegistrar`. Each job is idempotent, retryable (`[AutomaticRetry]`), and guarded against overlap (`[DisableConcurrentExecution]`); jobs dispatch a MediatR command and never embed business logic.

---

## Authentication

Provider:

* osu! OAuth

No local password authentication.

---

# High Level Architecture

```text
Next.js

    ↓

ASP.NET Core API

    ↓

Application Layer

    ↓

Domain Layer

    ↓

Infrastructure Layer

    ↓

PostgreSQL
Redis

    ↓

Hangfire Jobs

    ↓

osu! API
```

---

# Solution Structure

```text
src/

├── Api/
│
├── Application/
│
├── Domain/
│
├── Infrastructure/
│
├── Worker/
│
└── Shared/
```

---

# Clean Architecture Rules

Dependency Flow

```text
Infrastructure
        ↓

Api
        ↓

Application
        ↓

Domain
```

Rules:

1. Domain never depends on Application.

2. Domain never depends on Infrastructure.

3. Application never depends on Infrastructure implementations.

4. Infrastructure implements Domain and Application contracts.

5. Business logic belongs inside Domain.

---

# Domain Modules

## Identity

Responsibilities:

* User Accounts
* Authentication
* Authorization

---

## Player Registry

Responsibilities:

* Tracked Players
* Inclusion Rules
* Exclusion Rules

Admin controlled.

Only tracked players become stocks.

---

## Market

Core Domain

Responsibilities:

* Stock Pricing
* Price History
* Market Events
* Price Calculation
* Market Intelligence (read side): overview, stock list/details, OHLC candles (`history?range=`), stock analytics (volume / volatility / ownership / market cap), market + per-stock activity feed, trending (most bought/sold, fastest rising/falling, highest volume)

Only Market may modify stock prices. Market-intelligence endpoints are read-only projections and never mutate prices.

---

## Trading

Responsibilities:

* Buy Orders
* Sell Orders
* Holdings
* Portfolio

---

## Economy

Responsibilities:

* Wallet
* Credits
* Rewards
* Transaction Ledger

---

## Osu Integration

Responsibilities:

* OAuth
* API Client
* Snapshot Tracking
* Top Play Detection

This module never updates stock prices directly.

---

## Leaderboard

Responsibilities:

* Wealth ranking (wallet balance + market value of holdings)
* Profit ranking (net worth = wealth − net deposits)
* Trader ranking (traded credit volume in period)

Period-over-period change is derived from daily `WealthSnapshot` rows captured by the `wealth-snapshot` Hangfire job. Leaderboard reads go through `ILeaderboardReadRepository` and are short-TTL cached via `IReadModelCache`.

---

## Notifications

Responsibilities:

* Persist in-app notifications (`Notification` entity / `notifications` table)
* Holder fan-out: create a notification for every current holder of a stock when its player has a market-relevant event
* List / unread filter / mark-read / mark-all-read

Fan-out is implemented as additional in-process MediatR notification handlers reacting to `PpIncreased` and `TopPlayDetected` domain notifications. Real-time push delivery is out of scope (persist-and-poll).

---

# CQRS Strategy

Lightweight CQRS

Single Database

No Separate Read Database

Read-heavy features (leaderboards, market intelligence) use dedicated read repositories that project straight to read models / DTOs (set-based EF queries, `AsNoTracking()`). These are projections over the same PostgreSQL database, not a separate read store.

## Read-Model Caching

Hot read projections are cached behind `IReadModelCache` (Redis-backed, `GetOrSetAsync` with a short TTL, e.g. ~30s for leaderboards). Redis remains cache-only; PostgreSQL is the source of truth, so a cache miss or flush always re-derives from the database.

---

## Commands

CreateUserCommand

BuyStockCommand

SellStockCommand

GrantCurrencyCommand

AddTrackedPlayerCommand

DisableTrackedPlayerCommand

---

## Queries

GetPortfolioQuery

GetMarketQuery

GetLeaderboardQuery

GetPlayerStockQuery

GetWalletQuery

---

# Domain Events

Allowed:

TopPlayDetected

PPIncreased

PlayerInactive

BuyOrderExecuted

SellOrderExecuted

PriceChanged

RewardGranted

Events remain in-process.

Do not introduce Kafka.

Do not introduce message brokers.

Multiple handlers may react to one event. For example, `PpIncreased` and `TopPlayDetected` are consumed both by the Market Engine (price change) and by Notification fan-out handlers (notify current holders) — all dispatched in-process via MediatR notifications.

---

# osu! Synchronization Strategy

Important:

Never call osu! API from frontend requests.

Never call osu! API during stock page loads.

---

## Synchronization Process

Hangfire Job

Frequency:

Tier 1 Players:
Every 1 minute

Tier 2 Players:
Every 5 minutes

Tier 3 Players:
Every 15 minutes

Process:

1. Load tracked players
2. Fetch osu! data
3. Compare snapshots
4. Generate domain events
5. Update market

---

# Market Engine

Core Business Logic

Inputs:

BuyOrderExecuted

SellOrderExecuted

TopPlayDetected

PPIncreased

PlayerInactive

Outputs:

PriceChanged

---

## Price Formula

Version 1

Price =
BasePrice

* TradingImpact
* PerformanceImpact

- DecayImpact

Formula must remain configurable.

No hardcoded coefficients.

---

# Database Strategy

Single PostgreSQL Database

Initial Tables:

users

wallets

wallet_transactions

tracked_players

player_stocks

stock_price_history

portfolios

holdings

trades

player_snapshots

market_events

user_wealth_snapshots

notifications

New columns: `tracked_players.avatar_url`, `tracked_players.country_code`, `users.country_code`.

New indexes: `ix_trade_stock_executed`, `ix_stock_history_created`, `ix_wealth_snapshot_user_captured_desc`, `ix_notifications_user_created_desc`, `ix_notifications_user_unread`.

---

# Deployment

Version 1

Docker Compose

Services:

nextjs

api

worker

postgres

redis

hangfire-dashboard

nginx

---

# Development Phases

## Phase 1

Foundation

* OAuth
* Player Registry
* Wallet
* Trading
* Market
* Portfolio

Goal:

Playable MVP

---

## Phase 2

Economy Expansion

* Daily Rewards
* Achievements
* Notifications
* Enhanced Market History

---

## Phase 3

Competition Features

* Seasons
* Market Events
* Tournaments
* Investor Rankings

---

## Phase 4

Worker Separation

Deploy Hangfire workers independently.

No service extraction.

---

## Phase 5

Selective Service Extraction

Only if:

* More than 5000 concurrent users
* Multiple developers
* Independent scaling required

Potential candidates:

* Osu Integration
* Market Engine

---

# Codex / Claude Code Instructions

When generating code:

1. Follow Clean Architecture.

2. Follow DDD.

3. Follow CQRS.

4. Generate production-grade code.

5. Use MediatR for Commands and Queries.

6. Use EF Core with PostgreSQL.

7. Use FluentValidation.

8. Use constructor dependency injection.

9. Use Hangfire for scheduled jobs.

10. Use Redis for caching.

11. Do not generate microservices.

12. Do not generate Kafka.

13. Do not generate Kubernetes.

14. Keep everything inside a modular monolith.

15. Optimize for a solo developer workflow.

Primary Objective:

Ship gameplay quickly while maintaining enterprise-grade architecture.
