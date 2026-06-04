# DOMAIN_MODEL.md

# Core Domain

The core domain of osu! Stocks is the Market Engine.

The Market Engine determines stock prices based on:

* Player Performance
* Trading Activity
* Market Decay

All other modules support this domain.

---

# Aggregate: User

Purpose:

Represents an authenticated player.

Attributes:

* UserId
* OsuUserId
* Username
* AvatarUrl
* Role
* CreatedAt
* LastLoginAt

Rules:

* One osu! account maps to one user account.
* User cannot be deleted.
* User may be suspended.

---

# Aggregate: Wallet

Purpose:

Stores virtual currency.

Attributes:

* WalletId
* UserId
* CurrentBalance

Rules:

* Balance may never be negative.
* Balance changes only through transactions.

---

# Entity: WalletTransaction

Purpose:

Immutable financial ledger.

Attributes:

* TransactionId
* WalletId
* TransactionType
* Amount
* ReferenceId
* CreatedAt

Types:

* InitialGrant
* BuyStock
* SellStock
* DailyReward
* AdminGrant
* AdminDeduction

Rules:

* Immutable after creation.

---

# Aggregate: TrackedPlayer

Purpose:

Represents an osu! player tracked by the market.

Attributes:

* TrackedPlayerId
* OsuUserId
* Username
* TrackingTier
* IsActive
* CreatedAt

Rules:

* Only admins can create.
* Disabled players cannot be purchased.

---

# Aggregate: PlayerStock

Purpose:

Represents a tradeable stock.

Attributes:

* StockId
* TrackedPlayerId
* CurrentPrice
* DemandScore
* PerformanceScore
* LastUpdated

Rules:

* Price Floor = 1 Credit
* Price controlled only by Market Engine

---

# Entity: StockPriceHistory

Purpose:

Historical market tracking.

Attributes:

* PriceHistoryId
* StockId
* PreviousPrice
* NewPrice
* Reason
* CreatedAt

Reasons:

* BuyPressure
* SellPressure
* PPGain
* TopPlay
* Decay
* AdminAdjustment

---

# Aggregate: Portfolio

Purpose:

Represents user holdings.

Attributes:

* PortfolioId
* UserId

Rules:

* One portfolio per user.

---

# Entity: Holding

Purpose:

Represents ownership of a stock.

Attributes:

* HoldingId
* PortfolioId
* StockId
* Quantity
* AveragePrice

Rules:

* Quantity must be positive.
* Zero quantity holdings removed automatically.

---

# Aggregate: Trade

Purpose:

Represents a completed trade.

Attributes:

* TradeId
* UserId
* StockId
* TradeType
* Quantity
* UnitPrice
* TotalAmount
* ExecutedAt

Types:

* Buy
* Sell

Rules:

* Trades are immutable.
* Historical trades never modified.

---

# Aggregate: PlayerSnapshot

Purpose:

Stores cached osu! state.

Attributes:

* SnapshotId
* TrackedPlayerId
* CurrentPP
* GlobalRank
* TopScoreId
* TopScorePP
* CapturedAt

Rules:

* Created only by synchronization jobs.

---

# Domain Events

TopPlayDetected

PPIncreased

PlayerInactive

BuyOrderExecuted

SellOrderExecuted

PriceChanged

RewardGranted

PlayerDelisted

MarketMaintenanceEnabled
