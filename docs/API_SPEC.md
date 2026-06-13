# API_SPEC.md

# API Standards

Base URL

/api/v1

Authentication

Bearer Token (JWT issued after osu! OAuth). Send `Authorization: Bearer {jwt}`.

Content Type

application/json (responses are camelCase JSON; enums are serialized as their string names)

Rate limiting

Two fixed-window limiters (per `Program.cs`), returning `429 Too Many Requests` when exceeded:

- `auth` (10 requests / minute) — applied to the `/auth` group.
- `trading` (30 requests / minute) — applied to the `/trading` group.

Authorization groups

- Public: `/health`, `/api/v1/health`.
- Rate-limited only: `GET /auth/login`, `GET /auth/callback`.
- Authenticated (`RequireAuthorization`): `/auth/me`, `/market/*`, `/leaderboards/*`, `/trading/*`, `/portfolio/*`, `/wallet/*`, `/notifications/*`.
- Admin role (`RequireRole(Admin)`): `/admin/*` and the Hangfire dashboard at `/hangfire`.

---

# Authentication

## GET /auth/login

Purpose:

Redirect user to osu! OAuth. Rate-limited by the `auth` limiter.

Query Parameters:

`returnUrl` (optional)

- Must be an absolute `http` or `https` URL.
- Origin must be allow-listed in `Security:OAuthReturnUrl:AllowedOrigins`.
- `localhost` loopback origins are allowed in `Development` only.

Response:

302 Redirect (to the osu! authorization URL)

Errors:

400 `VALIDATION_ERROR` when `returnUrl` is invalid or not allow-listed

---

## GET /auth/callback

Purpose:

OAuth callback. Rate-limited by the `auth` limiter.

Query Parameters:

`code` (required), `state` (required)

Response:

- If a `returnUrl` was supplied at login, the browser is **302-redirected** back to that URL with the token in the URL fragment:
  `{returnUrl}#accessToken={jwt}&expiresAt={iso8601}`
- Otherwise, returns 200 with the token JSON:

{
"accessToken": "jwt-token",
"expiresAt": "2027-01-01T00:00:00Z",
"returnUrl": null
}

---

## GET /auth/me

Purpose:

Return current authenticated user. Requires authentication.

Response:

{
"userId": "uuid",
"osuUserId": 123456,
"username": "player",
"avatarUrl": "https://a.ppy.sh/123456",
"countryCode": "ID",
"role": "User",
"investorLevel": {
"level": 7,
"title": "Novice Investor",
"totalXp": 4820000,
"xpIntoLevel": 120000,
"xpForNextLevel": 360000,
"progressToNext": 0.333
}
}

`investorLevel` mirrors `GET /investor/level` (see Investor Levels). It is always present;
a user who has never traded reports level 1 with 0 XP.

---

# Market

All market endpoints require authentication.

## GET /market

Purpose:

Market overview. `topGainer`/`topLoser` are `{}` when no mover exists.

Response:

{
"totalStocks": 100,
"totalVolume": 500000,
"topGainer": {
"stockId": "uuid",
"playerName": "mrekk",
"avatarUrl": "https://a.ppy.sh/123456",
"currentPrice": 1500,
"priceChange24h": 4.5
},
"topLoser": {}
}

---

## GET /market/stocks

Purpose:

Paginated stock list.

Query Parameters:

page (default 1)

pageSize (default 25)

sort

search

Response:

{
"items": [
{
"stockId": "uuid",
"playerName": "mrekk",
"avatarUrl": "https://a.ppy.sh/123456",
"countryCode": "AU",
"currentPrice": 1500,
"volume": 25000,
"priceChange24h": 4.5
}
],
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
"avatarUrl": "https://a.ppy.sh/123456",
"countryCode": "AU",
"currentPrice": 1500,
"volume": 25000,
"priceChange24h": 4.5
}

---

## GET /market/stocks/{stockId}/history

Purpose:

Price history. When `range` is omitted, returns the raw price-point series. When `range` is supplied, returns OHLC candles for that window.

Query Parameters:

`range` (optional) — one of `1h`, `24h`, `7d`, `30d`. Bucket widths: `1h`→1-minute, `24h`→30-minute, `7d`→6-hour, `30d`→1-day candles.

Response (no `range` — raw series):

[
{
"timestamp": "2026-01-01T00:00:00Z",
"price": 1200
}
]

Response (with `range` — OHLC candles):

{
"range": "24h",
"candles": [
{
"bucketStart": "2026-01-01T00:00:00Z",
"open": 1200,
"high": 1260,
"low": 1180,
"close": 1240,
"volume": 3200
}
]
}

Errors:

400 `VALIDATION_ERROR` when `range` is not one of the supported tokens

---

## GET /market/stocks/{stockId}/analytics

Purpose:

Aggregate analytics for a single stock.

Response:

{
"volume24hShares": 4200,
"volume24hValue": 6300000,
"volume7dShares": 31000,
"volume7dValue": 46000000,
"volatility7d": 0.12,
"ownershipCount": 87,
"activeTraders24h": 23,
"marketCap": 150000000
}

---

## GET /market/events

Purpose:

Global market activity feed (price-moving events across all stocks).

Query Parameters:

page (default 1)

pageSize (default 25)

`type` (optional) — filters by event reason

Response:

{
"items": [
{
"stockId": "uuid",
"playerName": "mrekk",
"avatarUrl": "https://a.ppy.sh/123456",
"countryCode": "AU",
"reason": "ScoreSet",
"description": "Set a new top play",
"percentChange": 4.5,
"newPrice": 1500,
"occurredAt": "2026-01-01T00:00:00Z"
}
],
"page": 1,
"pageSize": 25
}

---

## GET /market/events/{stockId}

Purpose:

Activity feed scoped to a single stock.

Query Parameters:

page (default 1)

pageSize (default 25)

Response:

Same item shape as `GET /market/events`:

{
"items": [
{
"stockId": "uuid",
"playerName": "mrekk",
"avatarUrl": "https://a.ppy.sh/123456",
"countryCode": "AU",
"reason": "ScoreSet",
"description": "Set a new top play",
"percentChange": 4.5,
"newPrice": 1500,
"occurredAt": "2026-01-01T00:00:00Z"
}
],
"page": 1,
"pageSize": 25
}

---

## GET /market/trending

Purpose:

Trending stocks across several leaderboard-style sections. Each section is an array of stocks.

Query Parameters:

`window` (optional) — one of `1h`, `24h`, `7d`

`limit` (optional, default 10, max 50) — items per section

Response:

{
"mostBought": [
{
"stockId": "uuid",
"playerName": "mrekk",
"avatarUrl": "https://a.ppy.sh/123456",
"countryCode": "AU",
"metricValue": 4200,
"currentPrice": 1500
}
],
"mostSold": [],
"fastestRising": [],
"fastestFalling": [],
"highestVolume": []
}

Errors:

400 `VALIDATION_ERROR` when `window` is not one of `1h`/`24h`/`7d`, or `limit` is out of range

---

# Leaderboards

All leaderboard endpoints require authentication and share the same query parameters and response shape.

Query Parameters (all three):

`period` (optional) — one of `daily`, `weekly`, `monthly` (default `daily`)

page (default 1)

pageSize (default 25, max 100)

Response shape (all three):

{
"items": [
{
"rank": 1,
"userId": "uuid",
"username": "player",
"avatarUrl": "https://a.ppy.sh/123456",
"countryCode": "ID",
"value": 1250000,
"periodChange": 32000
}
],
"period": "daily",
"page": 1,
"pageSize": 25
}

`value`/`periodChange` semantics per endpoint:

- `GET /leaderboards/wealth` — total wealth (cash + holdings).
- `GET /leaderboards/profit` — realized/unrealized profit over the period.
- `GET /leaderboards/traders` — trading activity (e.g. volume/trade count) over the period.

Errors:

400 `VALIDATION_ERROR` when `period` is invalid or pagination is out of range

---

# Trading

The `/trading` group requires authentication and is rate-limited by the `trading` limiter.

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
"unitPrice": 1500,
"totalAmount": 150000,
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
"unitPrice": 1500,
"totalAmount": 75000,
"status": "Completed"
}

Errors:

400 InsufficientHoldings

404 StockNotFound

---

## GET /trading/history

Purpose:

User trade history.

Query Parameters:

page (default 1)

pageSize (default 25)

Response:

{
"items": [
{
"tradeId": "uuid",
"stockId": "uuid",
"tradeType": "Buy",
"quantity": 100,
"unitPrice": 1500,
"totalAmount": 150000,
"executedAt": "2026-01-01T00:00:00Z",
"playerName": "mrekk",
"avatarUrl": "https://a.ppy.sh/123456"
}
]
}

---

# Portfolio

The `/portfolio` group requires authentication.

## GET /portfolio

Purpose:

Portfolio summary.

Response:

{
"currentValue": 250000,
"costBasis": 235000,
"profitLoss": 15000,
"holdings": [
{
"holdingId": "uuid",
"stockId": "uuid",
"playerName": "mrekk",
"quantity": 100,
"averagePrice": 1000,
"currentPrice": 1500,
"costBasis": 100000,
"currentValue": 150000,
"profitLoss": 50000,
"avatarUrl": "https://a.ppy.sh/123456"
}
]
}

---

## GET /portfolio/holdings

Purpose:

Detailed holdings.

Response:

{
"items": [
{
"holdingId": "uuid",
"stockId": "uuid",
"playerName": "mrekk",
"quantity": 100,
"averagePrice": 1000,
"currentPrice": 1500,
"avatarUrl": "https://a.ppy.sh/123456"
}
]
}

---

# Wallet

The `/wallet` group requires authentication.

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

Query Parameters:

page (default 1)

pageSize (default 25)

Response:

{
"items": []
}

---

# Notifications

The `/notifications` group requires authentication. Notifications are fanned out to stock holders by background processes.

## GET /notifications

Purpose:

List the current user's notifications.

Query Parameters:

`unread` (optional, default false) — when true, only unread notifications are returned

page (default 1)

pageSize (default 25)

Response:

{
"items": [
{
"id": "uuid",
"type": "PriceAlert",
"title": "mrekk surged 12%",
"body": "A stock you hold moved sharply.",
"data": "{\"stockId\":\"uuid\"}",
"isRead": false,
"createdAt": "2026-01-01T00:00:00Z"
}
],
"page": 1,
"pageSize": 25
}

`data` is an optional JSON string payload (may be null).

---

## POST /notifications/{id}/read

Purpose:

Mark a single notification as read.

Response:

{
"success": true
}

Errors:

404 `NOT_FOUND` when the notification does not exist or does not belong to the user

---

## POST /notifications/read-all

Purpose:

Mark all of the current user's notifications as read.

Response:

{
"markedRead": 7
}

A level-up produces a notification with `type` `InvestorLevelUp` and a `data` payload of
`{"level":<int>,"title":"<string>"}` (see Investor Levels).

---

# Investor Levels

Investors earn XP from trading volume (1 XP per credit of gross traded value, on both buys and
sells). Levels follow an osu!-style curve: each level requires strictly more XP than the last,
with a soft cap at level 100 (every level beyond 100 costs a flat 100,000,000,000 XP, so
100 → 101 is a very large jump). Levels are cosmetic — they grant titles only, no gameplay perks.

The `/investor` group requires authentication and always operates on the current user.

## GET /investor/level

Purpose:

Return the current user's investor level standing. A user who has never traded reports
level 1 with 0 XP (never 404s).

Response:

{
"level": 7,
"title": "Novice Investor",
"totalXp": 4820000,
"xpIntoLevel": 120000,
"xpForNextLevel": 360000,
"progressToNext": 0.333
}

`progressToNext` is a 0..1 fraction. Title bands: 1–9 Novice Investor, 10–24 Retail Trader,
25–49 Active Trader, 50–74 Seasoned Investor, 75–99 Blue-Chip Trader, 100+ Market Legend.

---

# Admin

Authentication Required:

Role = Admin (also gates the Hangfire dashboard at `/hangfire`).

---

## GET /admin/tracked-players

Purpose:

List tracked players.

Query Parameters:

`isActive` (optional) — filter by active state

Response:

{
"items": []
}

---

## GET /admin/tracked-players/search

Purpose:

Search osu! players to add to tracking.

Query Parameters:

`query` (required)

`limit` (optional, default 10)

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
"osuUserId": 123456,
"trackingTier": "Tier3"
}

`trackingTier` is optional and defaults to `Tier3`.

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
"decayMultiplier": 1.0,
"isMaintenanceMode": false
}

---

## PUT /admin/market-settings

Purpose:

Update market configuration.

Request:

{
"ppMultiplier": 1.5,
"tradeMultiplier": 1.2,
"decayMultiplier": 0.5,
"isMaintenanceMode": false
}

Response:

204 No Content

---

# Health

## GET /health (also GET /api/v1/health)

Purpose:

Application health. Public (no auth). Checks PostgreSQL and Redis.

Response:

{
"status": "Healthy",
"checks": [
{
"name": "postgresql",
"status": "Healthy",
"duration": 12.3,
"description": null,
"exception": null
},
{
"name": "redis",
"status": "Healthy",
"duration": 4.1,
"description": null,
"exception": null
}
],
"totalDuration": 16.4
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


