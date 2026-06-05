using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OsuStocks.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCompositeReadPathIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_wallet_transactions_created_at",
                table: "wallet_transactions");

            migrationBuilder.DropIndex(
                name: "ix_wallet_transactions_wallet_id",
                table: "wallet_transactions");

            migrationBuilder.DropIndex(
                name: "ix_trade_executed",
                table: "trades");

            migrationBuilder.DropIndex(
                name: "ix_trade_user",
                table: "trades");

            migrationBuilder.DropIndex(
                name: "ix_stock_history_created",
                table: "stock_price_history");

            migrationBuilder.DropIndex(
                name: "ix_stock_history_stock",
                table: "stock_price_history");

            migrationBuilder.DropIndex(
                name: "ix_snapshot_player",
                table: "player_snapshots");

            migrationBuilder.DropIndex(
                name: "ix_snapshot_time",
                table: "player_snapshots");

            migrationBuilder.DropIndex(
                name: "ix_market_events_created",
                table: "market_events");

            migrationBuilder.DropIndex(
                name: "ix_market_events_stock",
                table: "market_events");

            migrationBuilder.CreateIndex(
                name: "ix_wallet_transactions_wallet_created_desc",
                table: "wallet_transactions",
                columns: new[] { "wallet_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_trade_user_executed_desc",
                table: "trades",
                columns: new[] { "user_id", "executed_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_tracked_players_active_tier_username",
                table: "tracked_players",
                columns: new[] { "is_active", "tracking_tier", "username" });

            migrationBuilder.CreateIndex(
                name: "ix_stock_history_stock_created_desc",
                table: "stock_price_history",
                columns: new[] { "stock_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_snapshot_player_captured_desc",
                table: "player_snapshots",
                columns: new[] { "tracked_player_id", "captured_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_market_events_stock_created_desc",
                table: "market_events",
                columns: new[] { "stock_id", "created_at" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_wallet_transactions_wallet_created_desc",
                table: "wallet_transactions");

            migrationBuilder.DropIndex(
                name: "ix_trade_user_executed_desc",
                table: "trades");

            migrationBuilder.DropIndex(
                name: "ix_tracked_players_active_tier_username",
                table: "tracked_players");

            migrationBuilder.DropIndex(
                name: "ix_stock_history_stock_created_desc",
                table: "stock_price_history");

            migrationBuilder.DropIndex(
                name: "ix_snapshot_player_captured_desc",
                table: "player_snapshots");

            migrationBuilder.DropIndex(
                name: "ix_market_events_stock_created_desc",
                table: "market_events");

            migrationBuilder.CreateIndex(
                name: "ix_wallet_transactions_created_at",
                table: "wallet_transactions",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_wallet_transactions_wallet_id",
                table: "wallet_transactions",
                column: "wallet_id");

            migrationBuilder.CreateIndex(
                name: "ix_trade_executed",
                table: "trades",
                column: "executed_at");

            migrationBuilder.CreateIndex(
                name: "ix_trade_user",
                table: "trades",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_history_created",
                table: "stock_price_history",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_stock_history_stock",
                table: "stock_price_history",
                column: "stock_id");

            migrationBuilder.CreateIndex(
                name: "ix_snapshot_player",
                table: "player_snapshots",
                column: "tracked_player_id");

            migrationBuilder.CreateIndex(
                name: "ix_snapshot_time",
                table: "player_snapshots",
                column: "captured_at");

            migrationBuilder.CreateIndex(
                name: "ix_market_events_created",
                table: "market_events",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_market_events_stock",
                table: "market_events",
                column: "stock_id");
        }
    }
}
