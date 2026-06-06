# Frontend API Contract

> Source of truth: actual implementation in `src/Api/Program.cs` and `src/Application/Features/`.
> Last updated: 2026-06-06.

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
4. The callback returns a JSON response with the JWT token.
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

Currently only `GET /market/stocks` returns the full pagination envelope. Other list endpoints return `{ "items": [...] }` without `page`/`totalCount`.

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

**Response:** `200 OK`

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIs...",
  "expiresAt": "2026-06-06T15:00:00+00:00",
  "returnUrl": "https://app.example.com/dashboard"
}
```

| Field | Type | Nullable | Description |
|-------|------|----------|-------------|
| `accessToken` | string | No | JWT token for subsequent requests |
| `expiresAt` | datetime | No | Token expiration timestamp |
| `returnUrl` | string | Yes | The `returnUrl` from the login step, if provided |

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
  "role": "Admin"
}
```

| Field | Type | Description |
|-------|------|-------------|
| `userId` | guid | Internal user ID |
| `osuUserId` | long | osu! user ID |
| `username` | string | osu! username |
| `role` | string | `"User"` or `"Admin"` |

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
    "currentPrice": 175.25,
    "priceChange24h": 5.25
  },
  "topLoser": {
    "stockId": "f0000000-0000-0000-0000-000000000008",
    "playerName": "Aricin",
    "currentPrice": 25.00,
    "priceChange24h": -5.00
  }
}
```

| Field | Type | Nullable | Description |
|-------|------|----------|-------------|
| `totalStocks` | int | No | Number of stocks in the market |
| `totalVolume` | long | No | Total trade volume |
| `topGainer` | object | Yes | Stock with highest 24h price change |
| `topLoser` | object | Yes | Stock with lowest 24h price change |

`topGainer` / `topLoser` fields:

| Field | Type | Description |
|-------|------|-------------|
| `stockId` | guid | Stock ID |
| `playerName` | string | osu! player name |
| `currentPrice` | decimal | Current stock price |
| `priceChange24h` | decimal | Absolute price change in last 24h |

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

| Field | Type | Description |
|-------|------|-------------|
| `stockId` | guid | Stock ID |
| `playerName` | string | osu! player name |
| `currentPrice` | decimal | Current price |
| `volume` | long | Trade volume |
| `priceChange24h` | decimal | Absolute price change in last 24h |

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
  "currentPrice": 175.25,
  "volume": 8,
  "priceChange24h": 5.25
}
```

**Errors:**

| Code | When |
|------|------|
| `NOT_FOUND` | Stock does not exist |

---

#### `GET /api/v1/market/stocks/{stockId}/history`

Returns the price history for a stock.

**Path parameters:**

| Param | Type | Required |
|-------|------|----------|
| `stockId` | guid | Yes |

**Response:** `200 OK`

```json
[
  {
    "timestamp": "2026-05-07T00:00:00+00:00",
    "price": 100.00
  },
  {
    "timestamp": "2026-05-17T00:00:00+00:00",
    "price": 135.00
  },
  {
    "timestamp": "2026-06-03T00:00:00+00:00",
    "price": 175.25
  }
]
```

Note: this endpoint returns a **bare array**, not wrapped in `{ "items": [...] }`.

| Field | Type | Description |
|-------|------|-------------|
| `timestamp` | datetime | Price change timestamp |
| `price` | decimal | Stock price at that point |

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
      "playerName": "mrekk"
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
      "profitLoss": 76.25
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
      "currentPrice": 175.25
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

1. Frontend navigates to `/api/v1/auth/login?returnUrl=https://yourapp.com/dashboard`.
2. The API **redirects** (302) to osu!'s OAuth page — this must happen in the browser's main window, not via `fetch()`.
3. After authorization, the user lands on `/api/v1/auth/callback?code=...&state=...`.
4. The callback returns JSON with `accessToken` and optionally `returnUrl`.
5. Frontend stores the token (e.g., localStorage or httpOnly cookie) and redirects to `returnUrl`.

**Important:** The `returnUrl` origin must be in the server's `Security:OAuthReturnUrl:AllowedOrigins` allow-list. In development, `localhost` origins are accepted automatically.

### Token Storage

- Store the `accessToken` from the callback response.
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
