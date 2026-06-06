# osu!Stocks — Frontend UI Spec & Hand-off

> **Companion to `design/prototype.html`** — open that file in any browser for the clickable visual.
> **Source of truth for the API:** [`docs/FRONTEND_API_CONTRACT.md`](../docs/FRONTEND_API_CONTRACT.md) (generated from `src/Api/Program.cs`).
> This document maps every screen/component to the exact endpoint, request/response shape, and UI state.

---

## 0. How to use this hand-off

1. Open **`prototype.html`** to see the intended layout, spacing, and visual language for every screen. Each screen is labelled with the endpoint(s) that feed it.
2. Use the **API contract** for exact field names, types, nullability, and error codes — do not guess; the contract is generated from the running code.
3. Build against the **live local API** at `http://localhost:5152` (a seeded dev database with the same data shown in the prototype: Cookiezi, mrekk, Vaxei, etc.).

**Stack-agnostic** — use React/Vue/Svelte/whatever. Suggested: React + TanStack Query (server state) + a charting lib (Recharts/visx) for the price history.

---

## 1. Design tokens

Copy these into your theme (CSS variables / Tailwind config). Full set is in the `:root{}` block of `prototype.html`.

| Token | Value | Use |
|---|---|---|
| `--bg` | `#0e0f13` | App background |
| `--surface` / `--surface-2` / `--surface-3` | `#171922` / `#1e212c` / `#262a37` | Cards, inputs, raised |
| `--border` | `#2b2f3c` | Hairlines |
| `--text` / `--muted` / `--faint` | `#e9eaf0` / `#9aa1b2` / `#6b7180` | Text hierarchy |
| `--accent` (osu! pink) | `#ff66aa` | Primary actions, brand, active nav |
| `--green` / `--red` | `#37d39b` / `#ff5d6c` | Gains / losses, buy / sell |
| `--amber` / `--blue` | `#ffb454` / `#5aa9ff` | Warnings / info |
| radius | `14px` card, `10px` control | — |
| font | Inter / system-ui; **mono** for all numbers, IDs, prices | — |

**Conventions**
- Prices/amounts: monospace, 2 decimals, `¢` prefix (credits) — e.g. `¢192.78`.
- Gains green with `+`, losses red; percentage badges (`+10.0%`) on a tinted pill.
- Enums render as their **string** value (`Tier1`, `Buy`, `Admin`) — the API both returns and now accepts these strings.

---

## 2. App shell & global behaviour

**Layout:** fixed left sidebar (nav, role-gated Admin section, user card) + top bar (page title, contextual search, wallet balance pill, avatar) + scrollable content. See prototype.

### Auth (do this first)
- **Login** is a full-screen page with a single “Sign in with osu!” button.
- The button must navigate the **browser window** (not `fetch`) to `GET /api/v1/auth/login?returnUrl=<your app url>`.
- API 302s to osu!; after consent the callback returns JSON `{ accessToken, expiresAt, returnUrl }`.
- Store `accessToken`; send `Authorization: Bearer <token>` on every authed request. Check `expiresAt` (default 120 min). On **401**, route back to login.
- `GET /api/v1/auth/me` → `{ userId, osuUserId, username, role }`. Use `role === "Admin"` to show the Admin nav section. Drive the user card + avatar (`https://a.ppy.sh/{osuUserId}`).

### Global states (every data view)
- **Loading:** skeleton rows/cards.
- **Empty:** centered icon + message (e.g. “No holdings yet — buy your first stock”).
- **Error:** toast using the API error shape `{ code, message, traceId }`. Map well-known `code`s (table in §5).
- **401:** redirect to login. **409 `CONCURRENCY_CONFLICT`:** auto-retry once, then toast.

---

## 3. Screen-by-screen

### 3.1 Market  ·  `GET /api/v1/market` + `GET /api/v1/market/stocks`
The default landing screen.
- **Overview cards (4):** Total Stocks, Total Volume, Top Gainer, Top Loser ← `GET /market` (`topGainer`/`topLoser` are **nullable** — render an empty state if null).
- **Stocks table** ← `GET /market/stocks?page&pageSize&sort&search`:
  - Columns: Player (avatar + name), Price, 24h Change (tinted pill), Volume, Trade button.
  - **Search** input → `search` param (debounce ~300ms). **Sort** controls → `sort` param. Valid values: `price_asc|price_desc|name_asc|name_desc|volume_asc|volume_desc|change24h_asc|change24h_desc`.
  - **Pagination:** this is the **only** endpoint returning the full envelope `{ items, page, pageSize, totalCount }` — build a real pager from it.
  - Row click → Stock Detail. Trade button → trade modal.

### 3.2 Stock Detail  ·  `GET /market/stocks/{stockId}` + `/history`
- **Header:** avatar, name, osu! id, tier, current price, 24h change.
- **Price chart** ← `/history` — returns a **bare array** `[{ timestamp, price }]` (not wrapped). Line/area chart.
- **Trade panel:** Buy/Sell segmented control, quantity stepper, live total (`unitPrice × qty`), ownership meter (vs 25% position limit), cooldown note. Submits via the trade modal.
- **404 `NOT_FOUND`** → “stock not found” empty state.

### 3.3 Trade modal (Buy / Sell)  ·  `POST /api/v1/trading/buy` | `/sell`
- Body `{ stockId, quantity }` (quantity > 0). Success → `{ tradeId, unitPrice, totalAmount, status:"Completed" }`.
- On success: toast, close, invalidate Wallet + Portfolio + Market queries.
- **Rate limited 30/min.** Handle these error codes inline (don’t just toast): `INVALID_STATE` (insufficient balance / shares), `TRADE_COOLDOWN` (show a 30s countdown on the button), `POSITION_LIMIT_EXCEEDED` (25% cap), `MAINTENANCE_MODE`, `NOT_FOUND`.

### 3.4 Portfolio  ·  `GET /api/v1/portfolio`
- **Summary cards (3):** Current Value, Cost Basis, Total P/L (green/red, with %).
- **Holdings table:** Player, Qty, Avg Price, Current, Cost Basis, Value, P/L (per-holding, color-coded).
- Empty state when `holdings` is `[]`.
- (`GET /portfolio/holdings` is a lighter list without valuations — use it where you only need raw positions, e.g. a sell-from dropdown.)

### 3.5 Wallet  ·  `GET /api/v1/wallet` + `/transactions`
- **Balance card** ← `GET /wallet` → `{ balance }`. Also feeds the top-bar balance pill.
- **Ledger table** ← `GET /wallet/transactions` (`{ items }`, paginated by `page`/`pageSize`): Type badge, Amount (signed: + credit green / − debit red), Reference (links to trade when `referenceId` set), Date. Transaction types: `InitialGrant, BuyStock, SellStock, DailyReward, AdminGrant, AdminDeduction`.

### 3.6 Trade History  ·  `GET /api/v1/trading/history`
- Table: Type badge (Buy/Sell), Player, Qty, Unit Price, Total, Executed date.
- Returns `{ items }` (no `totalCount`) — use simple `page`/`pageSize` “Load more” or numbered paging without a known total. `playerName` is nullable.

### 3.7 Admin — Tracked Players  *(role: Admin)*  ·  `GET /admin/tracked-players`
- List: Player, osu! id, Tier badge, Status (active/disabled), Tracked Since, action.
- Optional `?isActive=true|false` filter.
- **Disable/Enable** ← `PATCH …/{id}/disable` | `/enable` (→ 204). Optimistic toggle; revert on error.
- **Add player modal:**
  - Search ← `GET /admin/tracked-players/search?query&limit` (hits the live osu! API) → results with `isTracked` flag. Disable “Add” for already-tracked.
  - Add ← `POST /admin/tracked-players` body `{ osuUserId, trackingTier }` (tier default `"Tier3"`). Returns `{ trackedPlayerId }`. Error `INVALID_STATE` = already tracked.

### 3.8 Admin — Market Settings  *(role: Admin)*  ·  `GET` + `PUT /admin/market-settings`
- Form: `ppMultiplier`, `tradeMultiplier`, `decayMultiplier` (each 0–10), `isMaintenanceMode` toggle.
- Save ← `PUT` (→ **204 No Content**). When maintenance is on, surface a global banner app-wide (buy/sell will return `MAINTENANCE_MODE`).

---

## 4. Navigation map

```
/login                         → Login (unauthenticated)
/market            (default)   → 3.1 Market
/market/:stockId               → 3.2 Stock Detail
/portfolio                     → 3.4 Portfolio
/wallet                        → 3.5 Wallet
/history                       → 3.6 Trade History
/admin/players     (Admin)     → 3.7 Tracked Players
/admin/settings    (Admin)     → 3.8 Market Settings
```
Guard all routes except `/login` behind a valid token; guard `/admin/*` behind `role === "Admin"`.

---

## 5. Error-code → UI behaviour

| `code` | HTTP | UI |
|---|---|---|
| `VALIDATION_ERROR` | 400 | Inline field error |
| `INVALID_STATE` | 400 | Inline in trade modal (insufficient balance/shares) |
| `TRADE_COOLDOWN` | 400 | 30s countdown on the trade button |
| `POSITION_LIMIT_EXCEEDED` | 400 | Inline near ownership meter (25% cap) |
| `MAINTENANCE_MODE` | 400 | Global banner + disable trade actions |
| `UNAUTHORIZED` | 401 | Redirect to login |
| `FORBIDDEN` | 403 | “Admins only” empty state |
| `NOT_FOUND` | 404 | Resource empty state |
| `CONCURRENCY_CONFLICT` | 409 | Auto-retry once, then toast |
| `OSU_API_UNAVAILABLE` | 503 | Toast: “osu! is unreachable, try again” |
| `INTERNAL_ERROR` | 500 | Generic toast with `traceId` |

---

## 6. Component inventory (in the prototype)

Stat card · player cell (avatar + name) · change pill (up/down) · sortable table + pager · segmented control · quantity stepper · trade summary box · ownership meter · tier/status badges · toggle switch · modal · toast · empty state · price chart · search input · balance pill. All styled with the tokens in §1.

---

## 7. Notes & gotchas (from the live API)

- **Money/IDs are strings-friendly:** render decimals as-is (2dp); never do float math for display — trust the server’s computed `totalAmount`, `profitLoss`, etc.
- **Avatars:** `https://a.ppy.sh/{osuUserId}`.
- **Only `/market/stocks` has `totalCount`;** other lists are `{ items }` only.
- **`/market/stocks/{id}/history` is a bare array**, every other collection is wrapped in `{ items }`.
- **Rate limits:** auth 10/min, trading 30/min → handle `429`.
- **CORS:** API allows the origins in its `Cors:AllowedOrigins` (dev: `http://localhost:3000`). Tell backend your dev origin if different.
