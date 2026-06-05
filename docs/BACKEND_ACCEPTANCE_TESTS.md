# BACKEND_ACCEPTANCE_TESTS

This document defines backend acceptance tests for Phase 1 MVP modules so QA can verify behavior without reading source code.

## Environment

- API base URL: `http://localhost:5065/api/v1`
- Database: PostgreSQL
- Authentication: Bearer JWT from osu! OAuth callback
- Test tools: Swagger or Postman + SQL client

## Shared Test Data

Use these placeholders in requests and SQL:

- `<osu_user_id_user>`: normal user osu id
- `<osu_user_id_admin>`: admin osu id
- `<jwt_user>`: JWT for user
- `<jwt_admin>`: JWT for admin
- `<tracked_player_id>`: tracked player id
- `<stock_id>`: player stock id
- `<user_id>`: internal user UUID

---

## Authentication

### AUTH-001 OAuth Login Redirect

**Preconditions**
- `OsuOAuth` config is set (`ClientId`, `ClientSecret`, `RedirectUri`).

**Steps**
1. `GET /auth/login?returnUrl=http://localhost:3000/dashboard`
2. Observe response headers.

**Expected Result**
- HTTP `302`.
- `Location` header points to osu authorization URL and includes `state`.

**SQL Verification Queries**
```sql
-- No DB mutation expected in login redirect.
SELECT COUNT(*) AS users_count FROM users;
```

### AUTH-002 OAuth Callback Creates/Reuses User and Initializes Economy

**Preconditions**
- Valid authorization `code` and `state` from osu OAuth flow.

**Steps**
1. `GET /auth/callback?code=<code>&state=<state>`
2. Save `accessToken` from response.
3. Call `GET /auth/me` with token.

**Expected Result**
- Callback returns `200` with `accessToken`, `expiresAt`, and optional `returnUrl`.
- `/auth/me` returns `userId`, `osuUserId`, `username`, `role`.
- First login grants starting credits and creates wallet + portfolio once.

**SQL Verification Queries**
```sql
SELECT id, osu_user_id, username, role
FROM users
WHERE osu_user_id = <osu_user_id_user>;

SELECT w.id, w.user_id, w.balance
FROM wallets w
JOIN users u ON u.id = w.user_id
WHERE u.osu_user_id = <osu_user_id_user>;

SELECT wt.transaction_type, wt.amount, wt.created_at
FROM wallet_transactions wt
JOIN wallets w ON w.id = wt.wallet_id
JOIN users u ON u.id = w.user_id
WHERE u.osu_user_id = <osu_user_id_user>
ORDER BY wt.created_at DESC;

SELECT p.id, p.user_id
FROM portfolios p
JOIN users u ON u.id = p.user_id
WHERE u.osu_user_id = <osu_user_id_user>;
```

---

## Player Registry

### REG-001 Add Tracked Player

**Preconditions**
- Admin token `<jwt_admin>`.
- osu user is not already tracked.

**Steps**
1. `POST /admin/tracked-players` with `{ "osuUserId": <osu_user_id_target> }`
2. Save `trackedPlayerId`.
3. `GET /admin/tracked-players`.

**Expected Result**
- Create call returns `200` with `trackedPlayerId`.
- List includes new tracked player with active status.

**SQL Verification Queries**
```sql
SELECT id, osu_user_id, username, tracking_tier, is_active
FROM tracked_players
WHERE osu_user_id = <osu_user_id_target>;

SELECT ps.id, ps.tracked_player_id, ps.current_price
FROM player_stocks ps
JOIN tracked_players tp ON tp.id = ps.tracked_player_id
WHERE tp.osu_user_id = <osu_user_id_target>;
```

### REG-002 Disable and Enable Tracked Player

**Preconditions**
- Existing tracked player `<tracked_player_id>`.
- Admin token `<jwt_admin>`.

**Steps**
1. `PATCH /admin/tracked-players/<tracked_player_id>/disable`
2. `GET /admin/tracked-players`
3. `PATCH /admin/tracked-players/<tracked_player_id>/enable`
4. `GET /admin/tracked-players`

**Expected Result**
- Disable/enable return `204`.
- `isActive` toggles false then true.

**SQL Verification Queries**
```sql
SELECT id, is_active, updated_at, updated_by
FROM tracked_players
WHERE id = '<tracked_player_id>'::uuid;
```

---

## Synchronization

### SYNC-001 Snapshot Persistence and Event Detection

**Preconditions**
- Active tracked players exist.
- Worker/Hangfire is running.
- osu API access is available.

**Steps**
1. Trigger synchronization job (Hangfire recurring job or immediate run).
2. Wait for completion.
3. Re-run once after player performance change window or with test doubles.

**Expected Result**
- New rows in `player_snapshots` for active tracked players.
- When changes exist, market events are recorded (`PpIncreased`, `TopPlayDetected`, `PlayerInactive`).

**SQL Verification Queries**
```sql
SELECT tracked_player_id, COUNT(*) AS snapshot_count, MAX(captured_at) AS last_snapshot
FROM player_snapshots
GROUP BY tracked_player_id
ORDER BY last_snapshot DESC;

SELECT stock_id, event_type, created_at
FROM market_events
WHERE event_type IN ('PpIncreased', 'TopPlayDetected', 'PlayerInactive')
ORDER BY created_at DESC
LIMIT 50;
```

---

## Wallet

### WAL-001 Wallet Summary

**Preconditions**
- Authenticated user `<jwt_user>` with existing wallet.

**Steps**
1. `GET /wallet`

**Expected Result**
- HTTP `200`.
- Response contains non-negative `balance`.

**SQL Verification Queries**
```sql
SELECT w.balance
FROM wallets w
JOIN users u ON u.id = w.user_id
WHERE u.osu_user_id = <osu_user_id_user>;
```

### WAL-002 Wallet Ledger Immutability Through Trading

**Preconditions**
- User has wallet and at least one tradeable stock.

**Steps**
1. Execute `POST /trading/buy`.
2. Execute `POST /trading/sell`.
3. `GET /wallet/transactions`.

**Expected Result**
- Ledger includes appended rows for buy/sell.
- No previous rows are modified.

**SQL Verification Queries**
```sql
SELECT wt.id, wt.transaction_type, wt.amount, wt.reference_id, wt.created_at
FROM wallet_transactions wt
JOIN wallets w ON w.id = wt.wallet_id
JOIN users u ON u.id = w.user_id
WHERE u.osu_user_id = <osu_user_id_user>
ORDER BY wt.created_at DESC;
```

---

## Trading

### TRD-001 Buy Stock Success

**Preconditions**
- `<jwt_user>` valid.
- Stock active and user has enough balance.

**Steps**
1. `POST /trading/buy` with `{ "stockId": "<stock_id>", "quantity": 3 }`
2. `GET /trading/history`
3. `GET /portfolio/holdings`

**Expected Result**
- Buy returns `200` and `tradeId`.
- Trade history contains `Buy` entry.
- Holdings quantity increases.
- Wallet decreases.

**SQL Verification Queries**
```sql
SELECT id, user_id, stock_id, trade_type, quantity, unit_price, total_amount, executed_at
FROM trades
WHERE user_id = '<user_id>'::uuid
ORDER BY executed_at DESC
LIMIT 10;

SELECT h.id, h.portfolio_id, h.stock_id, h.quantity, h.average_price
FROM holdings h
JOIN portfolios p ON p.id = h.portfolio_id
WHERE p.user_id = '<user_id>'::uuid
  AND h.stock_id = '<stock_id>'::uuid;

SELECT balance FROM wallets WHERE user_id = '<user_id>'::uuid;
```

### TRD-002 Sell Stock Validation

**Preconditions**
- User owns holdings for `<stock_id>`.

**Steps**
1. `POST /trading/sell` with valid quantity.
2. Repeat with quantity larger than holdings.

**Expected Result**
- Valid sell returns `200`.
- Invalid sell returns `400` with `INSUFFICIENT_HOLDINGS`.

**SQL Verification Queries**
```sql
SELECT quantity
FROM holdings h
JOIN portfolios p ON p.id = h.portfolio_id
WHERE p.user_id = '<user_id>'::uuid
  AND h.stock_id = '<stock_id>'::uuid;
```

---

## Portfolio

### PTF-001 Portfolio Summary

**Preconditions**
- User has at least one holding.

**Steps**
1. `GET /portfolio`

**Expected Result**
- Response contains `currentValue`, `costBasis`, `profitLoss`, `holdings`.
- Values are consistent with holdings and current stock prices.

**SQL Verification Queries**
```sql
SELECT
    SUM(h.quantity * ps.current_price) AS current_value,
    SUM(h.quantity * h.average_price) AS cost_basis,
    SUM(h.quantity * ps.current_price) - SUM(h.quantity * h.average_price) AS profit_loss
FROM holdings h
JOIN portfolios p ON p.id = h.portfolio_id
JOIN player_stocks ps ON ps.id = h.stock_id
WHERE p.user_id = '<user_id>'::uuid;
```

### PTF-002 Portfolio Holdings Detail

**Preconditions**
- User has holdings.

**Steps**
1. `GET /portfolio/holdings`

**Expected Result**
- Response lists each holding with stock/player details, quantity, average price, current price.

**SQL Verification Queries**
```sql
SELECT
    h.stock_id,
    tp.username AS player_name,
    h.quantity,
    h.average_price,
    ps.current_price
FROM holdings h
JOIN portfolios p ON p.id = h.portfolio_id
JOIN player_stocks ps ON ps.id = h.stock_id
JOIN tracked_players tp ON tp.id = ps.tracked_player_id
WHERE p.user_id = '<user_id>'::uuid;
```

---

## Market Engine

### MKT-001 Buy/Sell Event Produces Price Change + History

**Preconditions**
- Market coefficients configured.
- User can buy/sell `<stock_id>`.

**Steps**
1. Record current stock price.
2. Execute `POST /trading/buy`.
3. Execute `POST /trading/sell`.
4. Query market-facing endpoints (if available) and DB.

**Expected Result**
- `player_stocks.current_price` changes according to configured coefficients.
- `stock_price_history` contains `BuyPressure` and `SellPressure` entries.
- `Price floor` is never violated.

**SQL Verification Queries**
```sql
SELECT id, current_price, last_updated
FROM player_stocks
WHERE id = '<stock_id>'::uuid;

SELECT stock_id, previous_price, new_price, reason, created_at
FROM stock_price_history
WHERE stock_id = '<stock_id>'::uuid
ORDER BY created_at DESC
LIMIT 20;

SELECT MIN(current_price) AS min_price_seen
FROM player_stocks;
```

### MKT-002 Performance and Inactivity Events Update Price and Record Reasons

**Preconditions**
- Synchronization is runnable.
- Tracked player with stock exists.

**Steps**
1. Trigger synchronization cycle(s).
2. Ensure at least one of these is produced: `PpIncreased`, `TopPlayDetected`, `PlayerInactive`.
3. Review price history and latest stock price.

**Expected Result**
- Each detected signal can drive a price update.
- `stock_price_history.reason` uses `PPGain`, `TopPlay`, or `Decay`.
- `PriceChanged` behavior is visible as persisted price progression.

**SQL Verification Queries**
```sql
SELECT me.stock_id, me.event_type, me.created_at
FROM market_events me
WHERE me.event_type IN ('PpIncreased', 'TopPlayDetected', 'PlayerInactive')
ORDER BY me.created_at DESC
LIMIT 50;

SELECT sph.stock_id, sph.previous_price, sph.new_price, sph.reason, sph.created_at
FROM stock_price_history sph
WHERE sph.reason IN ('PPGain', 'TopPlay', 'Decay')
ORDER BY sph.created_at DESC
LIMIT 50;
```

---

## Milestone Exit Checklist (Phase 1)

- Authentication works end-to-end with osu OAuth.
- Admin can add/enable/disable tracked players.
- Synchronization creates snapshots and market events.
- Wallet balance and ledger are correct and immutable.
- Trading buy/sell flows pass validations and persist trades/holdings.
- Portfolio summary and holdings are consistent with DB calculations.
- Market Engine updates prices from trade + performance + inactivity signals.
- Price floor rule (`>= 1`) is always enforced.
