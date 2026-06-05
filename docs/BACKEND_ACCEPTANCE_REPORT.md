# BACKEND_ACCEPTANCE_REPORT

## Scope

Re-evaluation of `docs/BACKEND_ACCEPTANCE_TESTS.md` after implementing previously **FAILED** and **PARTIAL** gaps.

Status legend:
- **PASS**: implemented and validated.
- **PARTIAL**: implemented in part, with known remaining gap.
- **FAIL**: not implemented.

## Per-Test Results

| Test ID | Status | Notes |
|---|---|---|
| AUTH-001 OAuth Login Redirect | PASS | Redirect flow remains implemented (`/api/v1/auth/login` -> 302 with `state`). |
| AUTH-002 OAuth Callback Creates/Reuses User and Initializes Economy | PASS | Callback creates/reuses user, initializes wallet/ledger/portfolio for first login, returns JWT. |
| REG-001 Add Tracked Player | PASS | Adding tracked player now also creates `player_stocks` row. Covered by integration test assertion. |
| REG-002 Disable and Enable Tracked Player | PASS | Toggle endpoints return `204` and update active status. |
| SYNC-001 Snapshot Persistence and Event Detection | PASS | Synchronization acceptance test now verifies snapshot persistence and event detection (`PpIncreased`, `TopPlayDetected`, `PlayerInactive`). |
| WAL-001 Wallet Summary | PASS | Wallet summary endpoint returns current non-negative balance. |
| WAL-002 Wallet Ledger Immutability Through Trading | PASS | Ledger remains append-only in application flow, and PostgreSQL trigger migration now blocks update/delete mutations on `wallet_transactions`. |
| TRD-001 Buy Stock Success | PASS | Buy flow updates trade history, holdings, and wallet. |
| TRD-002 Sell Stock Validation | PASS | Valid sell succeeds; invalid sell returns `INSUFFICIENT_HOLDINGS` with `400`. |
| PTF-001 Portfolio Summary | PASS | Portfolio valuation/profit-loss summary endpoint implemented and tested. |
| PTF-002 Portfolio Holdings Detail | PASS | Holdings endpoint returns expected stock/player/quantity/pricing fields. |
| MKT-001 Buy/Sell Event Produces Price Change + History | PASS | Integration test now verifies buy/sell generate `BuyPressure` and `SellPressure` history entries and floor is respected. |
| MKT-002 Performance/Inactivity Events Update Price + Reasons | PASS | Integration test now verifies sync signals drive market processing and persist `PPGain`, `TopPlay`, `Decay` history reasons. |

## Missing Endpoints

- None.

## Missing Business Rules

- None for current acceptance scope.

## Missing Validations

- None for current acceptance scope.

## Missing Database Behavior

- None for current acceptance scope.

## Missing Tests

- None for current acceptance scope.

## Verification

Executed and passing:
- `dotnet build OsuStocks.sln`
- `dotnet test OsuStocks.sln`
