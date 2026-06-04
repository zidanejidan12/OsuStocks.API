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
* username
* avatar_url
* role
* created_at
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

* ix_wallet_transactions_wallet_id
* ix_wallet_transactions_created_at

---

## tracked_players

Purpose:

Tracked osu! players.

Columns:

* id
* osu_user_id
* username
* tracking_tier
* is_active
* created_at

Indexes:

* uq_tracked_players_osu_user_id

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

* ix_stock_history_stock
* ix_stock_history_created

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

* ix_trade_user
* ix_trade_stock
* ix_trade_executed

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

* ix_snapshot_player
* ix_snapshot_time

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

* ix_market_events_stock
* ix_market_events_created

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
