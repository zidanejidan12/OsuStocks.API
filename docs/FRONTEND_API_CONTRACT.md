# Frontend API Contract

> Source of truth: actual implementation in `src/Api/Program.cs` and `src/Application/Features/`.
> Last updated: 2026-06-07.

---

## Base URL

```
http://localhost:5152   (local development)
http://localhost/api/v1  (Docker Compose via nginx)
```

All endpoints below are prefixed with `/api/v1` unless noted otherwise.

---

## Authentication

The API uses **JWT Bearer tokens** obtained via the osu! OAuth flow.

### How to authenticate

1. Redirect the user to `GET /api/v1/auth/login` (optionally with `?returnUrl=...`).
2. The API returns a **302 redirect** to osu!'s authorization page.
3. After the user authorizes, osu! redirects back to the API callback.
4. The callback behavior depends on whether a `returnUrl` was supplied at login:
   - **With `returnUrl`** (the normal SPA flow): the callback responds with a **302 redirect** to `<returnUrl>#accessToken=...&expiresAt=...` — the token is placed in the URL **fragment** (not the query string, so it stays out of server logs/history). The frontend reads the fragment and strips it.
   - **Without `returnUrl`**: the callback responds with **`200 OK`** and a JSON body containing the token.
5. Include the token in subsequent requests:

```
Authorization: Bearer <accessToken>
```

### Token format

The JWT contains:
- `sub` (NameIdentifier) — the user's internal GUID
- `unique_name` — the osu! username
- `role` — `User` or `Admin`
- Standard `iss`, `aud`, `exp` claims

---

## Common Patterns

### Error Response

All errors return a consistent JSON shape:

```json
{
  "code": "ERROR_CODE",
  "message": "Human-readable description.",
  "traceId": "00-abc123-def456-00"
}
```

| Code | HTTP Status | Description |
|------|-------------|-------------|
| `VALIDATION_ERROR` | 400 | Request validation failed |
| `INVALID_STATE` | 400 | Business rule violation (e.g., insufficient balance) |
| `TRADE_COOLDOWN` | 400 | Trade cooldown not elapsed |
| `POSITION_LIMIT_EXCEEDED` | 400 | Max ownership percentage exceeded |
| `MAINTENANCE_MODE` | 400 | Trading disabled during maintenance |
| `UNAUTHORIZED` | 401 | Missing or invalid token |
| `FORBIDDEN` | 403 | Insufficient permissions |
| `NOT_FOUND` | 404 | Resource does not exist |
| `CONCURRENCY_CONFLICT` | 409 | Resource modified by another request; retry |
| `OSU_API_UNAVAILABLE` | 503 | osu! API is unreachable |
| `INTERNAL_ERROR` | 500 | Unhandled server error |

### Pagination

Paginated endpoints accept `page` and `pageSize` query parameters and return:

```json
{
  "items": [...],
  "page": 1,
  "pageSize": 25,
  "totalCount": 150
}
```

- `page` — 1-based, default `1`
- `pageSize` — default `25`, max `100`

Pagination envelopes vary by endpoint:

- `GET /market/stocks` returns the **full** envelope: `items`, `page`, `pageSize`, `totalCount`.
- `GET /market/events`, `GET /market/events/{stockId}`, `GET /notifications`, and the three `GET /leaderboards/*` endpoints return `items`, `page`, `pageSize` (no `totalCount`). Leaderboards also include `period`.
- Other list endpoints (`/trading/history`, `/portfolio/holdings`, `/wallet/transactions`, `/admin/tracked-players`, `/admin/tracked-players/search`) return only `{ "items": [...] }`.

### Decimal Precision

- Prices and monetary amounts: 2 decimal places (e.g., `150.00`)
- Scores: 4 decimal places (e.g., `0.8500`)

### Timestamps

All timestamps are ISO 8601 with timezone offset:

```
"2026-06-06T03:00:00+00:00"
```

---

## Rate Limiting

| Route Group | Limit | Window | HTTP on exceed |
|-------------|-------|--------|----------------|
| `/api/v1/auth/*` | 10 requests | 1 minute | 429 Too Many Requests |
| `/api/v1/trading/*` | 30 requests | 1 minute | 429 Too Many Requests |
| All other endpoints | No limit | — | — |

Rate limits are per-client (IP-based).

---

## Endpoints

### Auth

#### `GET /api/v1/auth/login`

Initiates the osu! OAuth flow. Returns a **302 redirect** to osu!.

| | |
|---|---|
| Auth | None |
| Rate limited | Yes (auth) |

**Query parameters:**

| Param | Type | Required | Description |
|-------|------|----------|-------------|
| `returnUrl` | string | No | URL to redirect after login (max 2048 chars, origin must be allow-listed) |

**Response:** HTTP 302 redirect to `https://osu.ppy.sh/oauth/authorize?...`

**Errors:**

| Code | When |
|------|------|
| `VALIDATION_ERROR` | `returnUrl` origin not in allow-list |

---

#### `GET /api/v1/auth/callback`

Handles the osu! OAuth callback. Exchanges the authorization code for a JWT.

| | |
|---|---|
| Auth | None |
| Rate limited | Yes (auth) |

**Query parameters:**

| Param | Type | Required | Description |
|-------|------|----------|-------------|
| `code` | string | Yes | OAuth authorization code (max 512 chars) |
| `state` | string | Yes | OAuth state token (max 128 chars) |

**Response — when a `returnUrl` was provided at login:** `302 Found`

The browser is redirected to the SPA callback page with the token in the URL **fragment**:

```
Location: https://app.example.com/dashboard#accessToken=eyJhbGciOiJIUzI1NiIs...&expiresAt=2026-06-06T15%3A00%3A00.0000000%2B00%3A00
```

Both fragment values are URL-encoded. The frontend parses `window.location.hash`, stores the token, and clears the fragment.

**Response — when no `returnUrl` was provided:** `200 OK`

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIs...",
  "expiresAt": "2026-06-06T15:00:00+00:00",
  "returnUrl": null
}
```

| Field | Type | Nullable | Description |
|-------|------|----------|-------------|
| `accessToken` | string | No | JWT token for subsequent requests |
| `expiresAt` | datetime | No | Token expiration timestamp |
| `returnUrl` | string | Yes | Always `null` in this branch (a non-empty `returnUrl` triggers the 302 redirect instead) |

---

#### `GET /api/v1/auth/me`

Returns the authenticated user's profile.

| | |
|---|---|
| Auth | Bearer token |
| Rate limited | Yes (auth) |

**Response:** `200 OK`

```json
{
  "userId": "b0000000-0000-0000-0000-000000000001",
  "osuUserId": 4787150,
  "username": "Cookiezi",
  "avatarUrl": "https://a.ppy.sh/124493",
  "countryCode": "KR",
  "role": "Admin",
  "investorLevel": {
    "level": 7,
    "title": "Novice Investor",
    "totalXp": 4820000,
    "xpIntoLevel": 120000,
    "xpForNextLevel": 360000,
    "progressToNext": 0.333
  }
}
```

| Field | Type | Nullable | Description |
|-------|------|----------|-------------|
| `userId` | guid | No | Internal user ID |
| `osuUserId` | long | No | osu! user ID |
| `username` | string | No | osu! username |
| `avatarUrl` | string | Yes | osu! profile image URL |
| `countryCode` | string | Yes | ISO country code (e.g. `"KR"`) |
| `role` | string | No | `"User"` or `"Admin"` |
| `investorLevel` | object | No | Investor level standing (same shape as `GET /investor/level`) |

---

### Investor Levels

#### `GET /api/v1/investor/level`

Returns the authenticated user's investor level standing. Never 404s — a user who has never
traded reports level 1 with 0 XP.

| | |
|---|---|
| Auth | Bearer token |
| Rate limited | Yes (auth) |

**Response:** `200 OK`

```json
{
  "level": 7,
  "title": "Novice Investor",
  "totalXp": 4820000,
  "xpIntoLevel": 120000,
  "xpForNextLevel": 360000,
  "progressToNext": 0.333
}
```

| Field | Type | Nullable | Description |
|-------|------|----------|-------------|
| `level` | int | No | Current level (≥ 1; may exceed 100 in the soft-capped region) |
| `title` | string | No | Cosmetic title for the level band |
| `totalXp` | long | No | Lifetime XP earned (1 per credit of trade volume) |
| `xpIntoLevel` | long | No | XP earned since reaching the current level |
| `xpForNextLevel` | long | No | XP needed to advance from the current to the next level |
| `progressToNext` | number | No | Fraction `0..1` of progress toward the next level |

XP is earned on every buy and sell. A level-up additionally creates an `InvestorLevelUp`
notification.

---

### Market

All market endpoints require authentication.

#### `GET /api/v1/market`

Returns the market overview with total stocks, volume, and top movers.

**Response:** `200 OK`

```json
{
  "totalStocks": 8,
  "totalVolume": 50000,
  "topGainer": {
    "stockId": "f0000000-0000-0000-0000-000000000003",
    "playerName": "mrekk",
    "avatarUrl": "https://a.ppy.sh/7562902",
    "currentPrice": 175.25,
    "priceChange24h": 5.25
  },
  "topLoser": {
    "stockId": "f0000000-0000-0000-0000-000000000008",
    "playerName": "Aricin",
    "avatarUrl": "https://a.ppy.sh/8967394",
    "currentPrice": 25.00,
    "priceChange24h": -5.00
  }
}
```

| Field | Type | Nullable | Description |
|-------|------|----------|-------------|
| `totalStocks` | int | No | Number of stocks in the market |
| `totalVolume` | long | No | Total trade volume |
| `topGainer` | object | Yes | Stock with highest 24h price change (empty object `{}` when none) |
| `topLoser` | object | Yes | Stock with lowest 24h price change (empty object `{}` when none) |

`topGainer` / `topLoser` fields:

| Field | Type | Nullable | Description |
|-------|------|----------|-------------|
| `stockId` | guid | No | Stock ID |
| `playerName` | string | No | osu! player name |
| `avatarUrl` | string | Yes | osu! profile image URL |
| `currentPrice` | decimal | No | Current stock price |
| `priceChange24h` | decimal | No | Absolute price change in last 24h |

---

#### `GET /api/v1/market/stocks`

Returns a paginated, sortable, searchable list of stocks.

**Query parameters:**

| Param | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `page` | int | No | 1 | Page number (>0) |
| `pageSize` | int | No | 25 | Items per page (1–100) |
| `sort` | string | No | — | Sort order (see below) |
| `search` | string | No | — | Filter by player name |

**Supported sort values:**

`price_asc`, `price_desc`, `name_asc`, `name_desc`, `volume_asc`, `volume_desc`, `change24h_asc`, `change24h_desc`

**Response:** `200 OK`

```json
{
  "items": [
    {
      "stockId": "f0000000-0000-0000-0000-000000000003",
      "playerName": "mrekk",
      "avatarUrl": "https://a.ppy.sh/7562902",
      "countryCode": "AU",
      "currentPrice": 175.25,
      "volume": 8,
      "priceChange24h": 5.25
    }
  ],
  "page": 1,
  "pageSize": 25,
  "totalCount": 8
}
```

| Field | Type | Nullable | Description |
|-------|------|----------|-------------|
| `stockId` | guid | No | Stock ID |
| `playerName` | string | No | osu! player name |
| `avatarUrl` | string | Yes | osu! profile image URL |
| `countryCode` | string | Yes | ISO country code |
| `currentPrice` | decimal | No | Current price |
| `volume` | long | No | Trade volume |
| `priceChange24h` | decimal | No | Absolute price change in last 24h |

---

#### `GET /api/v1/market/stocks/{stockId}`

Returns details for a single stock.

**Path parameters:**

| Param | Type | Required |
|-------|------|----------|
| `stockId` | guid | Yes |

**Response:** `200 OK`

```json
{
  "stockId": "f0000000-0000-0000-0000-000000000003",
  "playerName": "mrekk",
  "avatarUrl": "https://a.ppy.sh/7562902",
  "countryCode": "AU",
  "currentPrice": 175.25,
  "volume": 8,
  "priceChange24h": 5.25
}
```

| Field | Type | Nullable | Description |
|-------|------|----------|-------------|
| `stockId` | guid | No | Stock ID |
| `playerName` | string | No | osu! player name |
| `avatarUrl` | string | Yes | osu! profile image URL |
| `countryCode` | string | Yes | ISO country code |
| `currentPrice` | decimal | No | Current price |
| `volume` | long | No | Trade volume |
| `priceChange24h` | decimal | No | Absolute price change in last 24h |

**Errors:**

| Code | When |
|------|------|
| `NOT_FOUND` | Stock does not exist |

---

#### `GET /api/v1/market/stocks/{stockId}/history`

Returns the price history for a stock. Behavior depends on the optional `range` query parameter:

- **No `range`** — returns the raw price-history points as a **bare array**.
- **With `range`** — returns aggregated **OHLC candles** wrapped in an object.

**Path parameters:**

| Param | Type | Required |
|-------|------|----------|
| `stockId` | guid | Yes |

**Query parameters:**

| Param | Type | Required | Description |
|-------|------|----------|-------------|
| `range` | string | No | One of `1h`, `24h`, `7d`, `30d`. Switches the response to OHLC candle mode. |

Candle bucket widths per range: `1h` → 1-minute, `24h` → 30-minute, `7d` → 6-hour, `30d` → 1-day.

**Response (no `range`):** `200 OK` — bare array

```json
[
  {
    "timestamp": "2026-05-07T00:00:00+00:00",
    "price": 100.00
  },
  {
    "timestamp": "2026-06-03T00:00:00+00:00",
    "price": 175.25
  }
]
```

| Field | Type | Description |
|-------|------|-------------|
| `timestamp` | datetime | Price change timestamp |
| `price` | decimal | Stock price at that point |

**Response (with `range`):** `200 OK`

```json
{
  "range": "24h",
  "candles": [
    {
      "bucketStart": "2026-06-06T00:00:00+00:00",
      "open": 170.00,
      "high": 178.00,
      "low": 168.50,
      "close": 175.25,
      "volume": 12
    }
  ]
}
```

| Field | Type | Description |
|-------|------|-------------|
| `range` | string | Echo of the normalized requested range |
| `candles[].bucketStart` | datetime | Start of the candle's time bucket |
| `candles[].open` | decimal | First price in the bucket |
| `candles[].high` | decimal | Highest price in the bucket |
| `candles[].low` | decimal | Lowest price in the bucket |
| `candles[].close` | decimal | Last price in the bucket |
| `candles[].volume` | long | Shares traded in the bucket |

**Errors:**

| Code | When |
|------|------|
| `VALIDATION_ERROR` | `range` is not one of the supported values |

---

#### `GET /api/v1/market/stocks/{stockId}/analytics`

Returns trading analytics for a single stock.

**Path parameters:**

| Param | Type | Required |
|-------|------|----------|
| `stockId` | guid | Yes |

**Response:** `200 OK`

```json
{
  "volume24hShares": 42,
  "volume24hValue": 7350.00,
  "volume7dShares": 310,
  "volume7dValue": 54200.00,
  "volatility7d": 0.1832,
  "ownershipCount": 17,
  "activeTraders24h": 9,
  "marketCap": 8762.50
}
```

| Field | Type | Description |
|-------|------|-------------|
| `volume24hShares` | long | Shares traded in last 24h |
| `volume24hValue` | decimal | Value traded in last 24h |
| `volume7dShares` | long | Shares traded in last 7 days |
| `volume7dValue` | decimal | Value traded in last 7 days |
| `volatility7d` | decimal | 7-day price volatility |
| `ownershipCount` | int | Number of holders |
| `activeTraders24h` | int | Distinct traders in last 24h |
| `marketCap` | decimal | Current price × total shares outstanding |

**Errors:**

| Code | When |
|------|------|
| `NOT_FOUND` | Stock does not exist |

---

#### `GET /api/v1/market/events`

Returns the market-wide activity feed (price-change events across all stocks).

**Query parameters:**

| Param | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `page` | int | No | 1 | Page number (>0) |
| `pageSize` | int | No | 25 | Items per page (1–100) |
| `type` | string | No | — | Optional filter by event `reason` (see PriceChangeReason enum) |

**Response:** `200 OK`

```json
{
  "items": [
    {
      "stockId": "f0000000-0000-0000-0000-000000000003",
      "playerName": "mrekk",
      "avatarUrl": "https://a.ppy.sh/7562902",
      "countryCode": "AU",
      "reason": "TopPlay",
      "description": "mrekk set a new top play",
      "percentChange": 3.25,
      "newPrice": 175.25,
      "occurredAt": "2026-06-07T02:15:00+00:00"
    }
  ],
  "page": 1,
  "pageSize": 25
}
```

| Field | Type | Nullable | Description |
|-------|------|----------|-------------|
| `stockId` | guid | No | Stock ID |
| `playerName` | string | No | osu! player name |
| `avatarUrl` | string | Yes | osu! profile image URL |
| `countryCode` | string | Yes | ISO country code |
| `reason` | string | No | Event reason (PriceChangeReason enum) |
| `description` | string | No | Human-readable event description |
| `percentChange` | decimal | No | Percentage price change for the event |
| `newPrice` | decimal | No | Price after the event |
| `occurredAt` | datetime | No | When the event happened |

---

#### `GET /api/v1/market/events/{stockId}`

Returns the activity feed scoped to a single stock. Same item shape and envelope as `GET /market/events`, but without the `type` filter.

**Path parameters:**

| Param | Type | Required |
|-------|------|----------|
| `stockId` | guid | Yes |

**Query parameters:**

| Param | Type | Required | Default |
|-------|------|----------|---------|
| `page` | int | No | 1 |
| `pageSize` | int | No | 25 |

**Response:** `200 OK` — identical shape to `GET /market/events` (`{ items, page, pageSize }`).

---

#### `GET /api/v1/market/trending`

Returns trending stocks bucketed into five sections.

**Query parameters:**

| Param | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `window` | string | No | `24h` | One of `1h`, `24h`, `7d` |
| `limit` | int | No | 10 | Items per section (1–50) |

**Response:** `200 OK`

```json
{
  "mostBought": [
    {
      "stockId": "f0000000-0000-0000-0000-000000000003",
      "playerName": "mrekk",
      "avatarUrl": "https://a.ppy.sh/7562902",
      "countryCode": "AU",
      "metricValue": 124,
      "currentPrice": 175.25
    }
  ],
  "mostSold": [],
  "fastestRising": [],
  "fastestFalling": [],
  "highestVolume": []
}
```

Each section is an array of:

| Field | Type | Nullable | Description |
|-------|------|----------|-------------|
| `stockId` | guid | No | Stock ID |
| `playerName` | string | No | osu! player name |
| `avatarUrl` | string | Yes | osu! profile image URL |
| `countryCode` | string | Yes | ISO country code |
| `metricValue` | decimal | No | The ranking metric for that section (e.g. shares bought, % change, volume) |
| `currentPrice` | decimal | No | Current stock price |

**Errors:**

| Code | When |
|------|------|
| `VALIDATION_ERROR` | `window` not one of `1h`/`24h`/`7d`, or `limit` outside 1–50 |

---

### Leaderboards

All leaderboard endpoints require authentication and share the same query parameters, item shape, and envelope. Three variants are exposed: `/leaderboards/wealth`, `/leaderboards/profit`, and `/leaderboards/traders`.

**Query parameters (all three):**

| Param | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `period` | string | No | `daily` | One of `daily`, `weekly`, `monthly`. Omitting it defaults to `daily`; an explicitly supplied value outside the set is rejected with `VALIDATION_ERROR` |
| `page` | int | No | 1 | Page number (>0) |
| `pageSize` | int | No | 25 | Items per page (1–100) |

**Response:** `200 OK`

```json
{
  "items": [
    {
      "rank": 1,
      "userId": "b0000000-0000-0000-0000-000000000001",
      "username": "Cookiezi",
      "avatarUrl": "https://a.ppy.sh/124493",
      "countryCode": "KR",
      "value": 18450.00,
      "periodChange": 1250.00
    }
  ],
  "period": "daily",
  "page": 1,
  "pageSize": 25
}
```

| Field | Type | Nullable | Description |
|-------|------|----------|-------------|
| `rank` | int | No | 1-based rank within the page sequence |
| `userId` | guid | No | Internal user ID |
| `username` | string | No | osu! username |
| `avatarUrl` | string | Yes | osu! profile image URL |
| `countryCode` | string | Yes | ISO country code |
| `value` | decimal | No | The leaderboard metric (total wealth / profit / trade count) |
| `periodChange` | decimal | Yes | Change in the metric over the selected period (null when no baseline) |

Top-level `period` echoes the normalized period used for the query.

---

### Trading

All trading endpoints require authentication and are rate limited (30 req/min).

#### `POST /api/v1/trading/buy`

Buys shares of a stock.

**Request body:**

```json
{
  "stockId": "f0000000-0000-0000-0000-000000000003",
  "quantity": 5
}
```

| Field | Type | Required | Validation |
|-------|------|----------|------------|
| `stockId` | guid | Yes | Must not be empty |
| `quantity` | int | Yes | Must be > 0 |

**Response:** `200 OK`

```json
{
  "tradeId": "30000000-0000-0000-0000-000000000001",
  "unitPrice": 175.25,
  "totalAmount": 876.25,
  "status": "Completed"
}
```

| Field | Type | Description |
|-------|------|-------------|
| `tradeId` | guid | Unique trade ID |
| `unitPrice` | decimal | Price per share at execution |
| `totalAmount` | decimal | Total cost (unitPrice × quantity) |
| `status` | string | Always `"Completed"` |

**Errors:**

| Code | When |
|------|------|
| `NOT_FOUND` | Stock does not exist |
| `INVALID_STATE` | Insufficient wallet balance |
| `TRADE_COOLDOWN` | Traded same stock too recently (default: 30s) |
| `POSITION_LIMIT_EXCEEDED` | Would exceed max ownership % (default: 25%) |
| `MAINTENANCE_MODE` | Market is in maintenance mode |

---

#### `POST /api/v1/trading/sell`

Sells shares of a stock.

**Request body:**

```json
{
  "stockId": "f0000000-0000-0000-0000-000000000003",
  "quantity": 2
}
```

| Field | Type | Required | Validation |
|-------|------|----------|------------|
| `stockId` | guid | Yes | Must not be empty |
| `quantity` | int | Yes | Must be > 0 |

**Response:** `200 OK`

```json
{
  "tradeId": "30000000-0000-0000-0000-000000000004",
  "unitPrice": 175.25,
  "totalAmount": 350.50,
  "status": "Completed"
}
```

**Errors:**

| Code | When |
|------|------|
| `NOT_FOUND` | Stock or holding does not exist |
| `INVALID_STATE` | Insufficient shares to sell |
| `TRADE_COOLDOWN` | Traded same stock too recently |
| `MAINTENANCE_MODE` | Market is in maintenance mode |

---

#### `GET /api/v1/trading/history`

Returns the authenticated user's trade history.

**Query parameters:**

| Param | Type | Required | Default |
|-------|------|----------|---------|
| `page` | int | No | 1 |
| `pageSize` | int | No | 25 |

**Response:** `200 OK`

```json
{
  "items": [
    {
      "tradeId": "30000000-0000-0000-0000-000000000001",
      "stockId": "f0000000-0000-0000-0000-000000000003",
      "tradeType": "Buy",
      "quantity": 5,
      "unitPrice": 160.00,
      "totalAmount": 800.00,
      "executedAt": "2026-05-17T00:00:00+00:00",
      "playerName": "mrekk",
      "avatarUrl": "https://a.ppy.sh/7562902"
    }
  ]
}
```

| Field | Type | Nullable | Description |
|-------|------|----------|-------------|
| `tradeId` | guid | No | Trade ID |
| `stockId` | guid | No | Stock ID |
| `tradeType` | string | No | `"Buy"` or `"Sell"` |
| `quantity` | int | No | Number of shares |
| `unitPrice` | decimal | No | Price per share |
| `totalAmount` | decimal | No | Total value |
| `executedAt` | datetime | No | Execution timestamp |
| `playerName` | string | Yes | osu! player name |
| `avatarUrl` | string | Yes | osu! profile image URL |

---

### Portfolio

All portfolio endpoints require authentication.

#### `GET /api/v1/portfolio`

Returns the user's portfolio summary with holdings, valuation, and profit/loss.

**Response:** `200 OK`

```json
{
  "currentValue": 1632.25,
  "costBasis": 1420.00,
  "profitLoss": 212.25,
  "holdings": [
    {
      "holdingId": "40000000-0000-0000-0000-000000000001",
      "stockId": "f0000000-0000-0000-0000-000000000003",
      "playerName": "mrekk",
      "quantity": 5,
      "averagePrice": 160.00,
      "currentPrice": 175.25,
      "costBasis": 800.00,
      "currentValue": 876.25,
      "profitLoss": 76.25,
      "avatarUrl": "https://a.ppy.sh/7562902"
    }
  ]
}
```

Top-level fields:

| Field | Type | Description |
|-------|------|-------------|
| `currentValue` | decimal | Total current value of all holdings |
| `costBasis` | decimal | Total cost basis of all holdings |
| `profitLoss` | decimal | Total profit/loss (currentValue − costBasis) |

Per-holding fields:

| Field | Type | Nullable | Description |
|-------|------|----------|-------------|
| `holdingId` | guid | No | Holding ID |
| `stockId` | guid | No | Stock ID |
| `playerName` | string | Yes | osu! player name |
| `quantity` | int | No | Shares held |
| `averagePrice` | decimal | No | Average purchase price |
| `currentPrice` | decimal | No | Current stock price |
| `costBasis` | decimal | No | quantity × averagePrice |
| `currentValue` | decimal | No | quantity × currentPrice |
| `profitLoss` | decimal | No | currentValue − costBasis |
| `avatarUrl` | string | Yes | osu! profile image URL |

---

#### `GET /api/v1/portfolio/holdings`

Returns a flat list of current holdings (without valuation calculations).

**Response:** `200 OK`

```json
{
  "items": [
    {
      "holdingId": "40000000-0000-0000-0000-000000000001",
      "stockId": "f0000000-0000-0000-0000-000000000003",
      "playerName": "mrekk",
      "quantity": 5,
      "averagePrice": 160.00,
      "currentPrice": 175.25,
      "avatarUrl": "https://a.ppy.sh/7562902"
    }
  ]
}
```

| Field | Type | Nullable | Description |
|-------|------|----------|-------------|
| `holdingId` | guid | No | Holding ID |
| `stockId` | guid | No | Stock ID |
| `playerName` | string | Yes | osu! player name |
| `quantity` | int | No | Shares held |
| `averagePrice` | decimal | No | Average purchase price |
| `currentPrice` | decimal | No | Current stock price |
| `avatarUrl` | string | Yes | osu! profile image URL |

---

### Wallet

All wallet endpoints require authentication.

#### `GET /api/v1/wallet`

Returns the user's current balance.

**Response:** `200 OK`

```json
{
  "balance": 8500.00
}
```

---

#### `GET /api/v1/wallet/transactions`

Returns the user's transaction ledger.

**Query parameters:**

| Param | Type | Required | Default |
|-------|------|----------|---------|
| `page` | int | No | 1 |
| `pageSize` | int | No | 25 |

**Response:** `200 OK`

```json
{
  "items": [
    {
      "transactionId": "20000000-0000-0000-0000-000000000001",
      "transactionType": "InitialGrant",
      "amount": 10000.00,
      "referenceId": null,
      "createdAt": "2026-05-07T00:00:00+00:00"
    },
    {
      "transactionId": "20000000-0000-0000-0000-000000000010",
      "transactionType": "BuyStock",
      "amount": -800.00,
      "referenceId": "30000000-0000-0000-0000-000000000001",
      "createdAt": "2026-05-17T00:00:00+00:00"
    }
  ]
}
```

| Field | Type | Nullable | Description |
|-------|------|----------|-------------|
| `transactionId` | guid | No | Transaction ID |
| `transactionType` | string | No | See WalletTransactionType enum |
| `amount` | decimal | No | Positive = credit, negative = debit |
| `referenceId` | guid | Yes | Linked trade ID (for buy/sell) |
| `createdAt` | datetime | No | Transaction timestamp |

---

### Notifications

All notification endpoints require authentication and operate on the current user's own notifications.

#### `GET /api/v1/notifications`

Returns the user's notifications, newest first.

**Query parameters:**

| Param | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `unread` | bool | No | false | When `true`, returns only unread notifications |
| `page` | int | No | 1 | Page number (>0) |
| `pageSize` | int | No | 25 | Items per page (1–100) |

**Response:** `200 OK`

```json
{
  "items": [
    {
      "id": "90000000-0000-0000-0000-000000000001",
      "type": "TopPlayDetected",
      "title": "mrekk set a new top play",
      "body": "mrekk set a new top play, which may move the price of your holding.",
      "data": "{\"trackedPlayerId\":\"e0000000-0000-0000-0000-000000000001\"}",
      "isRead": false,
      "createdAt": "2026-06-07T02:15:00+00:00"
    }
  ],
  "page": 1,
  "pageSize": 25
}
```

| Field | Type | Nullable | Description |
|-------|------|----------|-------------|
| `id` | guid | No | Notification ID |
| `type` | string | No | Notification type (e.g. `"TopPlayDetected"`, `"PpIncreased"`) |
| `title` | string | No | Short headline |
| `body` | string | No | Notification body text |
| `data` | string | Yes | JSON-encoded string with event-specific payload (parse client-side) |
| `isRead` | bool | No | Whether the user has read it |
| `createdAt` | datetime | No | When the notification was created |

Notifications are fanned out to all holders of a stock when relevant market events occur (e.g. a tracked player's PP increase or new top play).

---

#### `POST /api/v1/notifications/{id}/read`

Marks a single notification as read.

**Path parameters:**

| Param | Type | Required |
|-------|------|----------|
| `id` | guid | Yes |

**Response:** `200 OK`

```json
{ "success": true }
```

**Errors:**

| Code | When |
|------|------|
| `NOT_FOUND` | Notification does not exist or does not belong to the user |

---

#### `POST /api/v1/notifications/read-all`

Marks all of the user's unread notifications as read.

**Response:** `200 OK`

```json
{ "markedRead": 7 }
```

| Field | Type | Description |
|-------|------|-------------|
| `markedRead` | int | Number of notifications marked read |

---

### Admin

All admin endpoints require authentication with `Admin` role.

#### `GET /api/v1/admin/market-settings`

Returns global market configuration.

**Response:** `200 OK`

```json
{
  "ppMultiplier": 1.0000,
  "tradeMultiplier": 1.0000,
  "decayMultiplier": 1.0000,
  "isMaintenanceMode": false
}
```

---

#### `PUT /api/v1/admin/market-settings`

Updates global market configuration.

**Request body:**

```json
{
  "ppMultiplier": 1.5,
  "tradeMultiplier": 0.75,
  "decayMultiplier": 0.85,
  "isMaintenanceMode": false
}
```

| Field | Type | Validation |
|-------|------|------------|
| `ppMultiplier` | decimal | 0–10 inclusive |
| `tradeMultiplier` | decimal | 0–10 inclusive |
| `decayMultiplier` | decimal | 0–10 inclusive |
| `isMaintenanceMode` | bool | — |

**Response:** `204 No Content`

---

#### `GET /api/v1/admin/tracked-players`

Lists tracked players.

**Query parameters:**

| Param | Type | Required | Description |
|-------|------|----------|-------------|
| `isActive` | bool | No | Filter by active status |

**Response:** `200 OK`

```json
{
  "items": [
    {
      "trackedPlayerId": "e0000000-0000-0000-0000-000000000001",
      "osuUserId": 4787150,
      "username": "Cookiezi",
      "trackingTier": "Tier1",
      "isActive": true,
      "createdAt": "2026-04-07T00:00:00+00:00",
      "updatedAt": null
    }
  ]
}
```

| Field | Type | Nullable | Description |
|-------|------|----------|-------------|
| `trackedPlayerId` | guid | No | Internal ID |
| `osuUserId` | long | No | osu! user ID |
| `username` | string | No | osu! username |
| `trackingTier` | string | No | `"Tier1"`, `"Tier2"`, or `"Tier3"` |
| `isActive` | bool | No | Whether actively tracked |
| `createdAt` | datetime | No | When tracking started |
| `updatedAt` | datetime | Yes | Last modification |

---

#### `GET /api/v1/admin/tracked-players/search`

Searches the osu! API for players to track.

**Query parameters:**

| Param | Type | Required | Default | Validation |
|-------|------|----------|---------|------------|
| `query` | string | Yes | — | Max 100 chars |
| `limit` | int | No | 10 | 1–50 |

**Response:** `200 OK`

```json
{
  "items": [
    {
      "osuUserId": 4787150,
      "username": "Cookiezi",
      "avatarUrl": "https://a.ppy.sh/4787150",
      "currentPp": 16600.00,
      "globalRank": 13,
      "isTracked": true,
      "trackedPlayerId": "e0000000-0000-0000-0000-000000000001",
      "isActive": true
    }
  ]
}
```

| Field | Type | Nullable | Description |
|-------|------|----------|-------------|
| `osuUserId` | long | No | osu! user ID |
| `username` | string | No | osu! username |
| `avatarUrl` | string | Yes | Profile image URL |
| `currentPp` | decimal | No | Performance points |
| `globalRank` | int | Yes | Global ranking |
| `isTracked` | bool | No | Already tracked in system |
| `trackedPlayerId` | guid | Yes | Internal ID if tracked |
| `isActive` | bool | Yes | Active status if tracked |

---

#### `POST /api/v1/admin/tracked-players`

Adds a player to tracking. Creates a corresponding stock.

**Request body:**

```json
{
  "osuUserId": 4787150,
  "trackingTier": "Tier1"
}
```

| Field | Type | Required | Default | Validation |
|-------|------|----------|---------|------------|
| `osuUserId` | long | Yes | — | Must be > 0 |
| `trackingTier` | string | No | `"Tier3"` | Must be valid tier |

**Response:** `200 OK`

```json
{
  "trackedPlayerId": "e0000000-0000-0000-0000-000000000001"
}
```

Note: the endpoint returns only `trackedPlayerId`, not the full response DTO.

**Errors:**

| Code | When |
|------|------|
| `INVALID_STATE` | Player already tracked |
| `OSU_API_UNAVAILABLE` | Cannot fetch player from osu! API |

---

#### `PATCH /api/v1/admin/tracked-players/{id}/enable`

Re-enables a disabled tracked player.

**Response:** `204 No Content`

**Errors:** `NOT_FOUND` if player does not exist.

---

#### `PATCH /api/v1/admin/tracked-players/{id}/disable`

Disables a tracked player (stops syncing).

**Response:** `204 No Content`

**Errors:** `NOT_FOUND` if player does not exist.

---

### Health

#### `GET /health` and `GET /api/v1/health`

Returns application and dependency health status.

| | |
|---|---|
| Auth | None |
| Rate limited | No |

**Response:** `200 OK` (healthy) or `503 Service Unavailable` (unhealthy)

```json
{
  "status": "Healthy",
  "checks": [
    {
      "name": "postgresql",
      "status": "Healthy",
      "duration": 12.34,
      "description": null,
      "exception": null
    },
    {
      "name": "redis",
      "status": "Healthy",
      "duration": 5.67,
      "description": null,
      "exception": null
    }
  ],
  "totalDuration": 15.80
}
```

---

## Enums

These enums are serialized as **strings** in all API responses.

### UserRole

| Value | Description |
|-------|-------------|
| `User` | Regular user |
| `Admin` | Administrator (access to admin endpoints + Hangfire dashboard) |

### TradeType

| Value | Description |
|-------|-------------|
| `Buy` | Stock purchase |
| `Sell` | Stock sale |

### WalletTransactionType

| Value | Description |
|-------|-------------|
| `InitialGrant` | Starting balance on account creation |
| `BuyStock` | Debit from buying shares |
| `SellStock` | Credit from selling shares |
| `DailyReward` | Daily login reward (not yet implemented) |
| `AdminGrant` | Admin-issued credit |
| `AdminDeduction` | Admin-issued debit |

### TrackingTier

| Value | Sync Frequency | Description |
|-------|---------------|-------------|
| `Tier1` | Every 1 minute | Top players, highest priority |
| `Tier2` | Every 5 minutes | Active players |
| `Tier3` | Every 15 minutes | All other tracked players |

### PriceChangeReason

Used in price history (not directly exposed via API, but visible in stock history context):

| Value | Description |
|-------|-------------|
| `BuyPressure` | Price increased due to buy orders |
| `SellPressure` | Price decreased due to sell orders |
| `PPGain` | Price increased due to PP gain |
| `TopPlay` | Price increased due to new top play |
| `Decay` | Price decreased due to inactivity |
| `AdminAdjustment` | Manual admin price adjustment |

---

## Web UI Integration Notes

### OAuth Login Flow

1. Frontend navigates to `/api/v1/auth/login?returnUrl=https://yourapp.com/auth/callback`.
2. The API **redirects** (302) to osu!'s OAuth page — this must happen in the browser's main window, not via `fetch()`.
3. After authorization, the user lands on `/api/v1/auth/callback?code=...&state=...`.
4. Because a `returnUrl` was supplied, the API **redirects** (302) the browser to `<returnUrl>#accessToken=...&expiresAt=...` — the token is in the URL **fragment**.
5. The SPA callback page reads `window.location.hash`, extracts `accessToken`/`expiresAt`, stores the token (e.g. localStorage), clears the fragment, and routes the user onward.

If `returnUrl` is omitted at step 1, the callback instead returns `200 OK` with a JSON body (`{ accessToken, expiresAt, returnUrl: null }`) — useful for non-browser clients, but the SPA should always pass a `returnUrl` so it gets the 302+fragment flow.

**Important:** The `returnUrl` origin must be in the server's `Security:OAuthReturnUrl:AllowedOrigins` allow-list. In development, `localhost` origins are accepted automatically.

### Token Storage

- Store the `accessToken` from the callback redirect fragment (or JSON body for the no-`returnUrl` branch).
- Include it as `Authorization: Bearer <token>` on all authenticated requests.
- Check `expiresAt` to handle token expiry (default: 120 minutes).
- On 401 responses, redirect the user back to the login flow.

### CORS

The API allows cross-origin requests only from origins listed in `Cors:AllowedOrigins`. In development, `http://localhost:3000` and `https://localhost:3000` are configured by default.

Allowed methods: all. Allowed headers: all. Credentials: allowed.

### Optimistic Concurrency

Trading and wallet operations use optimistic concurrency. If two requests modify the same resource simultaneously, one will receive:

```json
{
  "code": "CONCURRENCY_CONFLICT",
  "message": "The resource was modified by another request. Please retry.",
  "traceId": "..."
}
```

**Recommended handling:** Retry the request once. If it fails again, show an error to the user.

### Maintenance Mode

When `isMaintenanceMode` is `true` in market settings, all buy/sell requests return:

```json
{
  "code": "MAINTENANCE_MODE",
  "message": "Trading is currently disabled for maintenance."
}
```

Frontend should check `GET /admin/market-settings` or handle this error gracefully with a maintenance banner.

### Anti-Abuse Constraints

| Constraint | Default | User-Facing Behavior |
|------------|---------|---------------------|
| Trade cooldown | 30 seconds per stock | Show countdown timer after each trade |
| Position limit | 25% max ownership | Show ownership % before buy confirmation |

### Swagger

Available at `/swagger` in development environments. Disabled in production unless `Security:EnableSwagger=true`.

### Hangfire Dashboard

Available at `/hangfire`. Requires `Admin` role and HTTPS in production.
