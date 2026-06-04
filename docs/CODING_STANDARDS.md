# CODING_STANDARDS.md

# Purpose

This document defines coding conventions, architectural constraints, and development standards for osu! Stocks.

All generated code must comply with these standards.

---

# General Principles

1. Readability over cleverness.

2. Simplicity over abstraction.

3. Explicit code over magic.

4. Business logic belongs in the Domain.

5. Infrastructure must never contain business rules.

6. Controllers must remain thin.

7. Handlers coordinate use cases but do not contain business logic.

---

# Clean Architecture Rules

Allowed Dependencies:

Domain
← Application
← Api

Domain
← Infrastructure

Rules:

* Domain must not reference any external framework.
* Domain must not reference EF Core.
* Domain must not reference ASP.NET.
* Domain must not reference Redis.

Application may reference:

* Domain
* MediatR
* FluentValidation

Infrastructure may reference:

* EF Core
* Redis
* External APIs

---

# Vertical Slice Architecture

Organize features by business capability.

Preferred Structure:

Features/

├── Trading/
│   ├── BuyStock/
│   ├── SellStock/
│
├── Wallet/
│   ├── GetWallet/
│   ├── GrantCredits/
│
├── Market/
│   ├── GetMarketOverview/
│   ├── GetStockDetails/

Each slice contains:

* Command or Query
* Handler
* Validator
* Endpoint
* DTOs

---

# CQRS Standards

Every write operation must be a Command.

Every read operation must be a Query.

Examples:

BuyStockCommand

SellStockCommand

AddTrackedPlayerCommand

GetPortfolioQuery

GetMarketQuery

---

# MediatR Usage

All requests must flow through MediatR.

Controllers may not directly call repositories.

Bad:

Controller → Repository

Good:

Controller → MediatR → Handler

---

# Validation

Use FluentValidation.

Validation must occur before handler execution.

Example validations:

* Quantity > 0
* User authenticated
* Wallet balance available

Business validation remains inside Domain.

---

# Error Handling

Do not use exceptions for business rule violations.

Use Result Pattern.

Example:

Result.Success()

Result.Failure("INSUFFICIENT_BALANCE")

Exceptions are reserved for:

* Infrastructure failures
* Unexpected failures
* Third-party failures

---

# Domain Rules

Domain Entities must enforce invariants.

Example:

Holding cannot have negative quantity.

Wallet cannot have negative balance.

PlayerStock cannot fall below price floor.

Never trust handler validation alone.

---

# Entity Framework Rules

Use:

* Code First
* Migrations

Avoid:

* Lazy Loading

Prefer:

* Explicit Includes

Use:

AsNoTracking()

for read-only queries.

---

# Repository Standards

Repositories belong to Domain contracts.

Implementation belongs to Infrastructure.

Repositories should expose business-oriented methods.

Avoid generic repositories.

Bad:

IGenericRepository<T>

Good:

IPlayerStockRepository

ITradeRepository

IWalletRepository

---

# API Standards

REST API only.

Versioned routes:

/api/v1

All endpoints return:

* Success response
* Standard error response

---

# Logging

Use structured logging.

Required fields:

* UserId
* StockId
* TradeId
* TraceId

Never log:

* Access tokens
* Secrets
* Connection strings

---

# Background Jobs

Use Hangfire.

All jobs must be:

* Idempotent
* Retryable

Jobs must tolerate duplicate execution.

---

# Redis Usage

Allowed:

* Caching
* Rate limiting
* Temporary data

Not Allowed:

* Source of truth

PostgreSQL remains the source of truth.

---

# Testing Standards

Required:

Unit Tests

For:

* Market Engine
* Trading Logic
* Wallet Logic

Recommended:

Integration Tests

For:

* API endpoints
* Database interactions

---

# Naming Standards

Classes:

PascalCase

Methods:

PascalCase

Variables:

camelCase

Constants:

PascalCase

Database:

snake_case

---

# AI Generation Rules

When generating code:

1. Follow Clean Architecture.
2. Follow Vertical Slice Architecture.
3. Use MediatR.
4. Use FluentValidation.
5. Use Result Pattern.
6. Use EF Core.
7. Use PostgreSQL.
8. Use Redis only for caching.
9. Never introduce microservices.
10. Never introduce Kafka.
11. Never introduce Kubernetes.
12. Optimize for a solo developer workflow.
