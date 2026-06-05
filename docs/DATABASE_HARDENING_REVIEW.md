# Database Hardening Review

Date: 2026-06-06
Reviewer Role: PostgreSQL Architect
Scope: Full review of all domain entities and EF Core migrations in src/Domain/Entities and src/Infrastructure/Persistence/Migrations.

## Executive Summary

The schema has a solid baseline with primary keys, core foreign keys, and key one-to-one/one-to-many uniqueness constraints. It also has an immutability trigger for wallet_transactions and optimistic concurrency columns for wallets, holdings, and player_stocks.

Main hardening gaps remain in four areas:

1. No explicit CHECK constraints are defined in schema/migrations (NO_CHECK_CONSTRAINT_DEFINITIONS_FOUND from repository scan).
2. Singleton/business uniqueness is not fully enforced (notably market_settings and trade-linked wallet ledger idempotency).
3. wallet_transactions.reference_id is not protected by a foreign key.
4. Concurrency protection is partial (tokens only on three mutable aggregates; no concurrency conflict handling path).

## Files Reviewed

Entities:

- `src/Domain/Entities/User.cs`
- `src/Domain/Entities/Wallet.cs`
- `src/Domain/Entities/WalletTransaction.cs`
- `src/Domain/Entities/Portfolio.cs`
- `src/Domain/Entities/Holding.cs`
- `src/Domain/Entities/Trade.cs`
- `src/Domain/Entities/TrackedPlayer.cs`
- `src/Domain/Entities/PlayerStock.cs`
- `src/Domain/Entities/PlayerSnapshot.cs`
- `src/Domain/Entities/StockPriceHistory.cs`
- `src/Domain/Entities/MarketEvent.cs`
- `src/Domain/Entities/MarketSettings.cs`

Migrations:

- `src/Infrastructure/Persistence/Migrations/20260604075733_InitialCreate.cs`
- `src/Infrastructure/Persistence/Migrations/20260605092734_AddMarketSettings.cs`
- `src/Infrastructure/Persistence/Migrations/20260605094836_EnforceWalletTransactionImmutability.cs`
- `src/Infrastructure/Persistence/Migrations/20260605141353_AddOptimisticConcurrencyTokens.cs`
- `src/Infrastructure/Persistence/Migrations/20260605165937_AddCompositeReadPathIndexes.cs`
- `src/Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs`

## Current Strengths (Already Present)

- Core foreign keys are created for modeled relationships in initial migration (for example `FK_trades_users_user_id`, `FK_holdings_portfolios_portfolio_id`, `FK_wallet_transactions_wallets_wallet_id`) in `20260604075733_InitialCreate.cs:68-266`.
- Strong uniqueness exists for core identities:
  - `uq_users_osu_user_id` in `UserConfiguration.cs:25`
  - `uq_tracked_players_osu_user_id` in `TrackedPlayerConfiguration.cs:24`
  - `uq_wallet_user_id` in `WalletConfiguration.cs:27`
  - `uq_portfolio_user` in `PortfolioConfiguration.cs:21`
  - `uq_player_stock_player` in `PlayerStockConfiguration.cs:30`
  - `uq_holding_portfolio_stock` in `HoldingConfiguration.cs:29`
- `wallet_transactions` immutability is protected by trigger/function in `20260605094836_EnforceWalletTransactionImmutability.cs:13-40`.
- Optimistic concurrency columns exist for `wallets`, `holdings`, and `player_stocks` via `row_version` in `20260605141353_AddOptimisticConcurrencyTokens.cs:14-31` and concurrency token config lines:
  - `WalletConfiguration.cs:18-21`
  - `HoldingConfiguration.cs:20-23`
  - `PlayerStockConfiguration.cs:20-23`

## Missing CHECK Constraints

No check constraints are defined in entity configurations or migrations (`NO_CHECK_CONSTRAINT_DEFINITIONS_FOUND`).

### High ROI checks to add

1. Financial non-negativity

- `wallets.balance >= 0` (`WalletConfiguration.cs:16`)
- `holdings.quantity > 0` and `holdings.average_price >= 0` (`HoldingConfiguration.cs:17-18`)
- `trades.quantity > 0`, `unit_price > 0`, `total_amount > 0` (`TradeConfiguration.cs:18-20`)
- `wallet_transactions.amount > 0` (`WalletTransactionConfiguration.cs:17`)
- `player_stocks.current_price > 0` (`PlayerStockConfiguration.cs:16`)
- `stock_price_history.previous_price >= 0` and `new_price >= 0` (`StockPriceHistoryConfiguration.cs:16-17`)

2. Domain enum integrity for string-converted enums

- `users.role` (`UserConfiguration.cs:18`)
- `tracked_players.tracking_tier` (`TrackedPlayerConfiguration.cs:17`)
- `trades.trade_type` (`TradeConfiguration.cs:17`)
- `wallet_transactions.transaction_type` (`WalletTransactionConfiguration.cs:16`)
- `stock_price_history.reason` (`StockPriceHistoryConfiguration.cs:18`)

3. Settings range guarantees at DB level

- `market_settings` multipliers are validated in app (`UpdateMarketSettingsCommandValidator.cs:9-16`) but not in DB (`MarketSettingsConfiguration.cs:15-17`).
- Recommended DB checks: each multiplier between `0` and `10`.

4. Positive identifier/rank sanity

- `users.osu_user_id > 0` and `tracked_players.osu_user_id > 0` (`UserConfiguration.cs:15`, `TrackedPlayerConfiguration.cs:15`)
- `player_snapshots.global_rank IS NULL OR global_rank > 0` (`PlayerSnapshotConfiguration.cs:17`)

## Missing UNIQUE Constraints

1. market_settings singleton protection (high)

- Table currently has only PK (AddMarketSettings.cs:15-29) and retrieval is latest-row logic (MarketSettingsRepository.cs:11-22).
- This allows multiple rows and non-deterministic historical drift.
- Recommendation: enforce one-row policy, for example a unique index on a constant expression.

2. trade-linked wallet ledger idempotency uniqueness (high)

- wallet_transactions.reference_id exists (WalletTransactionConfiguration.cs:18) and trade flows set it from trade.Id (BuyStockCommandHandler.cs:121, SellStockCommandHandler.cs:94), but no uniqueness constraint prevents duplicate posting for the same trade.
- Recommendation: partial unique index for trade-backed transaction types.

3. snapshot deduplication (medium)

- No uniqueness on (tracked_player_id, captured_at); only an index exists (PlayerSnapshotConfiguration.cs:22-24).
- If synchronization is retried/replayed at the same timestamp window, duplicate snapshots are possible.

## Missing Foreign Key Constraints

1. wallet_transactions.reference_id has no FK to trades.id (high)

- Column is nullable UUID (WalletTransactionConfiguration.cs:18, AppDbContextModelSnapshot.cs:586-588), but the only FK on wallet_transactions is wallet_id (20260604075733_InitialCreate.cs:265-269).
- Buy/Sell flows use this field as trade reference (BuyStockCommandHandler.cs:121, SellStockCommandHandler.cs:94).
- Missing referential integrity allows orphaned or invalid reference IDs.

Recommended structural fix:

- Introduce explicit nullable trade_id with FK to trades(id) for trade-linked entries.
- Keep non-trade references separate, or use typed reference columns plus check constraints by transaction type.

## Missing Concurrency Protections

Current optimistic concurrency is limited to 3 entities (Wallet, Holding, PlayerStock) via IHasRowVersion (IHasRowVersion.cs:3, entity implementations in Wallet.cs:5, Holding.cs:5, PlayerStock.cs:5).

### Gaps

1. Mutable entities without row-version protection

- TrackedPlayer is updated by enable/disable handlers (EnableTrackedPlayerCommandHandler.cs:25-30, DisableTrackedPlayerCommandHandler.cs:25-30) but has no concurrency token.
- User is updated during OAuth callback (HandleOsuCallbackCommandHandler.cs:86-97) but has no concurrency token.
- MarketSettings is updated in admin flow (UpdateMarketSettingsCommandHandler.cs:39-47) but has no concurrency token.

2. No concurrency exception handling strategy

- No DbUpdateConcurrencyException handling path exists in application layer (repository-wide scan found none).

3. App-managed row_version only

- row_version increment is performed in AppDbContext.SaveChangesAsync (AppDbContext.cs:33-61), not by DB trigger.
- Any out-of-band SQL update can bypass version increment semantics.

## Priority Hardening Plan

### P0 (Immediate)

1. Add CHECK constraints for financial positivity and enum-domain integrity.
2. Add singleton unique guarantee for market_settings.
3. Add FK-safe model for trade-linked wallet references (trade_id + FK).

### P1 (Short Term)

1. Extend row-version concurrency to tracked_players, users, and market_settings.
2. Add application-level handling for DbUpdateConcurrencyException with conflict translation.

### P2 (Stability and Idempotency)

1. Add partial unique index to prevent duplicate trade-linked wallet postings.
2. Add optional dedup uniqueness for snapshots if operationally required.

## Example PostgreSQL DDL (Illustrative)

```sql
ALTER TABLE wallets
  ADD CONSTRAINT ck_wallets_balance_non_negative CHECK (balance >= 0);

ALTER TABLE trades
  ADD CONSTRAINT ck_trades_quantity_positive CHECK (quantity > 0),
  ADD CONSTRAINT ck_trades_price_positive CHECK (unit_price > 0),
  ADD CONSTRAINT ck_trades_total_positive CHECK (total_amount > 0),
  ADD CONSTRAINT ck_trades_type CHECK (trade_type IN ('Buy', 'Sell'));

ALTER TABLE market_settings
  ADD CONSTRAINT ck_market_settings_pp_multiplier_range CHECK (pp_multiplier BETWEEN 0 AND 10),
  ADD CONSTRAINT ck_market_settings_trade_multiplier_range CHECK (trade_multiplier BETWEEN 0 AND 10),
  ADD CONSTRAINT ck_market_settings_decay_multiplier_range CHECK (decay_multiplier BETWEEN 0 AND 10);

CREATE UNIQUE INDEX uq_market_settings_singleton ON market_settings ((true));

CREATE UNIQUE INDEX uq_wallet_transactions_trade_ref
  ON wallet_transactions (reference_id, transaction_type)
  WHERE reference_id IS NOT NULL
    AND transaction_type IN ('BuyStock', 'SellStock');
```
