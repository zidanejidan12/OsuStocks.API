# API_SPEC.md

# API Standards

Base URL

/api/v1

Authentication

Bearer Token (osu! OAuth)

Content Type

application/json

---

# Authentication

## GET /auth/login

Purpose:

Redirect user to osu! OAuth.

Response:

302 Redirect

---

## GET /auth/callback

Purpose:

OAuth callback.

Response:

Authentication token.

Example:

{
"accessToken": "jwt-token",
"expiresAt": "2027-01-01T00:00:00Z"
}

---

## GET /auth/me

Purpose:

Return current authenticated user.

Response:

{
"userId": "uuid",
"osuUserId": 123456,
"username": "player",
"role": "User"
}

---

# Market

## GET /market

Purpose:

Market overview.

Response:

{
"totalStocks": 100,
"totalVolume": 500000,
"topGainer": {},
"topLoser": {}
}

---

## GET /market/stocks

Purpose:

Paginated stock list.

Query Parameters:

page

pageSize

sort

search

Response:

{
"items": [],
"page": 1,
"pageSize": 25,
"totalCount": 100
}

---

## GET /market/stocks/{stockId}

Purpose:

Stock details.

Response:

{
"stockId": "uuid",
"playerName": "mrekk",
"currentPrice": 1500,
"volume": 25000,
"priceChange24h": 4.5
}

---

## GET /market/stocks/{stockId}/history

Purpose:

Price history.

Response:

[
{
"timestamp": "2026-01-01T00:00:00Z",
"price": 1200
}
]

---

# Trading

## POST /trading/buy

Purpose:

Buy stock.

Request:

{
"stockId": "uuid",
"quantity": 100
}

Response:

{
"tradeId": "uuid",
"status": "Completed"
}

Errors:

400 InvalidQuantity

400 InsufficientBalance

404 StockNotFound


---

## POST /trading/sell

Purpose:

Sell stock.

Request:

{
"stockId": "uuid",
"quantity": 50
}

Response:

{
"tradeId": "uuid",
"status": "Completed"
}

Errors:

400 InsufficientHoldings

404 StockNotFound

---

## GET /trading/history

Purpose:

User trade history.

Response:

{
"items": []
}

---

# Portfolio

## GET /portfolio

Purpose:

Portfolio summary.

Response:

{
"currentValue": 250000,
"profitLoss": 15000,
"holdings": []
}

---

## GET /portfolio/holdings

Purpose:

Detailed holdings.

Response:

[
{
"stockId": "uuid",
"playerName": "mrekk",
"quantity": 100,
"averagePrice": 1000,
"currentPrice": 1500
}
]

---

# Wallet

## GET /wallet

Purpose:

Wallet summary.

Response:

{
"balance": 100000
}

---

## GET /wallet/transactions

Purpose:

Wallet ledger.

Response:

{
"items": []
}

---

# Postponed Endpoints (Phase 1.5)

Leaderboards are postponed and are not part of the current MVP release scope.

Planned after MVP:

- `GET /leaderboards/richest`
- `GET /leaderboards/investors`
- `GET /leaderboards/stocks`
- Trading maintenance-mode behavior (including `409 MarketMaintenance`)

---

# Admin

Authentication Required:

Role = Admin

---

## GET /admin/tracked-players

Purpose:

List tracked players.

Response:

{
"items": []
}

---

## POST /admin/tracked-players

Purpose:

Add tracked player.

Request:

{
"osuUserId": 123456
}

Response:

{
"trackedPlayerId": "uuid"
}

---

## PATCH /admin/tracked-players/{id}/disable

Purpose:

Disable tracked player.

Response:

204 No Content

---

## PATCH /admin/tracked-players/{id}/enable

Purpose:

Enable tracked player.

Response:

204 No Content

---

## GET /admin/market-settings

Purpose:

Retrieve market configuration.

Response:

{
"ppMultiplier": 1.0,
"tradeMultiplier": 1.0,
"decayMultiplier": 1.0
}

---

## PUT /admin/market-settings

Purpose:

Update market configuration.

Request:

{
"ppMultiplier": 1.5,
"tradeMultiplier": 1.2,
"decayMultiplier": 0.5
}

Response:

204 No Content

---

# Health

## GET /health

Purpose:

Application health.

Response:

{
"status": "Healthy"
}

---

# Error Response Standard

Format:

{
"code": "INSUFFICIENT_BALANCE",
"message": "Wallet balance is insufficient.",
"traceId": "guid"
}

---

# Versioning Strategy

URI Versioning

Example:

/api/v1

Future:

/api/v2

Breaking changes require new version.

