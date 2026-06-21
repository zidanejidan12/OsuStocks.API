using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OsuStocks.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BackendAuditFixes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_wealth_snapshot_user_captured_desc",
                table: "user_wealth_snapshots");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "last_inactivity_decay_at",
                table: "tracked_players",
                type: "timestamp with time zone",
                nullable: true);

            // The unique index below would fail if a prior (non-idempotent) job run left
            // duplicate (user_id, captured_at) rows. Collapse any such duplicates to the
            // earliest row per group before enforcing uniqueness, so the migration is safe
            // to apply against existing data.
            migrationBuilder.Sql(@"
                DELETE FROM user_wealth_snapshots
                WHERE ctid NOT IN (
                    SELECT MIN(ctid)
                    FROM user_wealth_snapshots
                    GROUP BY user_id, captured_at
                );");

            migrationBuilder.CreateIndex(
                name: "uq_wealth_snapshot_user_captured",
                table: "user_wealth_snapshots",
                columns: new[] { "user_id", "captured_at" },
                unique: true,
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_wealth_snapshot_user_captured",
                table: "user_wealth_snapshots");

            migrationBuilder.DropColumn(
                name: "last_inactivity_decay_at",
                table: "tracked_players");

            migrationBuilder.CreateIndex(
                name: "ix_wealth_snapshot_user_captured_desc",
                table: "user_wealth_snapshots",
                columns: new[] { "user_id", "captured_at" },
                descending: new[] { false, true });
        }
    }
}
