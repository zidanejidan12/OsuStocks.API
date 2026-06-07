# DATABASE.md

# Database Strategy

Database Engine:

PostgreSQL

Architecture:

Single Database

Schema Strategy:

Single schema initially.

Future schemas may be introduced if needed.

---

# Tables

## users

Purpose:

Authenticated users.

Columns:

* id (uuid)
* osu_user_id (bigint)
* username (varchar 64)
* avatar_url (varchar 512, nullable)
* country_code (varchar 2, nullable)
* role (varchar 16, enum-as-string)
* created_at
* created_by
* updated_at
* updated_by
* last_login_at

Indexes:

* uq_users_osu_user_id
* ix_users_username

---

## wallets

Purpose:

User balance.

Columns:

* id
* user_id
* balance
* created_at

Indexes:

* uq_wallet_user_id

---

## wallet_transactions

Purpose:

Immutable ledger.

Columns:

* id
* wallet_id
* transaction_type
* amount
* reference_id
* created_at

Indexes:

* ix_wallet_transactions_wallet_created_desc (wallet_id, created_at DESC)

---

## tracked_players

Purpose:

Tracked osu! players.

Columns:

* id
* osu_user_id
* username (varchar 64)
* avatar_url (varchar 512, nullable)
* country_code (varchar 2, nullable)
* tracking_tier (varchar 16, enum-as-string)
* is_active
* created_at
* created_by
* updated_at
* updated_by

Indexes:

* uq_tracked_players_osu_user_id
* ix_tracked_players_active_tier_username

---

## player_stocks

Purpose:

Market stocks.

Columns:

* id
* tracked_player_id
* current_price
* demand_score
* performance_score
* last_updated

Indexes:

* uq_player_stock_player
* ix_player_stock_price

---

## stock_price_history

Purpose:

Historical prices.

Columns:

* id
* stock_id
* previous_price
* new_price
* reason
* created_at

Indexes:

* ix_stock_history_stock_created_desc (stock_id, created_at DESC)
* ix_stock_history_created (created_at DESC)

---

## portfolios

Purpose:

User portfolio.

Columns:

* id
* user_id
* created_at

Indexes:

* uq_portfolio_user

---

## holdings

Purpose:

Current ownership.

Columns:

* id
* portfolio_id
* stock_id
* quantity
* average_price

Indexes:

* uq_holding_portfolio_stock
* ix_holding_stock

---

## trades

Purpose:

Trade history.

Columns:

* id
* user_id
* stock_id
* trade_type
* quantity
* unit_price
* total_amount
* executed_at

Indexes:

* ix_trade_stock
* ix_trade_user_executed_desc (user_id, executed_at DESC)
* ix_trade_stock_executed (stock_id, executed_at DESC)

---

## player_snapshots

Purpose:

Cached osu! state.

Columns:

* id
* tracked_player_id
* current_pp
* global_rank
* top_score_id
* top_score_pp
* captured_at

Indexes:

* ix_snapshot_player_captured_desc (tracked_player_id, captured_at DESC)

---

## market_events

Purpose:

Audit trail.

Columns:

* id
* stock_id
* event_type
* payload
* created_at

Indexes:

* ix_market_events_stock_created_desc (stock_id, created_at DESC)

---

## market_settings

Purpose:

Singleton market tuning knobs.

Columns:

* id (uuid)
* pp_multiplier (numeric 10,4)
* trade_multiplier (numeric 10,4)
* decay_multiplier (numeric 10,4)
* is_maintenance_mode (bool, default false)
* created_at
* created_by
* updated_at
* updated_by

Indexes:

* (primary key only)

---

## user_wealth_snapshots

Purpose:

Daily per-user wealth/profit snapshots captured by the wealth-snapshot background job; backs wealth-history charts and profit leaderboards.

Columns:

* id (uuid)
* user_id (uuid)
* captured_at (timestamptz)
* wealth (numeric 18,2)
* net_deposits (numeric 18,2)
* profit (numeric 18,2)

Indexes:

* ix_wealth_snapshot_user_captured_desc (user_id, captured_at DESC)

---

## notifications

Purpose:

Per-user notifications (e.g. holder fan-out on market events). Backs list / unread-count / mark-read endpoints.

Columns:

* id (uuid)
* user_id (uuid)
* type (varchar 64)
* title (varchar 200)
* body (varchar 1000)
* data (jsonb, nullable)
* is_read (bool)
* created_at (timestamptz)

Indexes:

* ix_notifications_user_created_desc (user_id, created_at DESC)
* ix_notifications_user_unread (user_id, is_read)

---

# Foreign Keys

wallets.user_id
→ users.id

tracked_players.id
→ player_stocks.tracked_player_id

portfolios.user_id
→ users.id

holdings.portfolio_id
→ portfolios.id

holdings.stock_id
→ player_stocks.id

trades.user_id
→ users.id

trades.stock_id
→ player_stocks.id

player_snapshots.tracked_player_id
→ tracked_players.id

market_events.stock_id
→ player_stocks.id

Note: `user_wealth_snapshots.user_id` and `notifications.user_id` reference users logically but have NO database-level foreign-key constraint (no FK is declared in their EF configuration or migration). They are indexed by user_id instead.

---

# Migration Chain

Applied in order (Infrastructure/Persistence/Migrations):

1. 20260604075733_InitialCreate
2. 20260605092734_AddMarketSettings
3. 20260605094836_EnforceWalletTransactionImmutability
4. 20260605141353_AddOptimisticConcurrencyTokens
5. 20260605165937_AddCompositeReadPathIndexes
6. 20260606034441_AddMarketMaintenanceMode
7. 20260607085732_AddReadModelIndexes — adds ix_trade_stock_executed, ix_stock_history_created
8. 20260607092901_AddWealthSnapshots — adds user_wealth_snapshots (+ ix_wealth_snapshot_user_captured_desc)
9. 20260607095754_AddPlayerUserAvatarCountry — adds users.country_code, tracked_players.avatar_url, tracked_players.country_code
10. 20260607111642_AddNotifications — adds notifications (+ ix_notifications_user_created_desc, ix_notifications_user_unread)

---

# Audit Columns Standard

Every mutable table should include:

* created_at
* created_by
* updated_at
* updated_by

Immutable tables only require:

* created_at

Examples:

* trades
* wallet_transactions
* stock_price_history
* market_events
