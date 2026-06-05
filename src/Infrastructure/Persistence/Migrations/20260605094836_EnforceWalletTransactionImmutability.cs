using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OsuStocks.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnforceWalletTransactionImmutability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION prevent_wallet_transactions_mutation()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    RAISE EXCEPTION 'wallet_transactions is immutable';
                END;
                $$;
                """);

            migrationBuilder.Sql(
                """
                CREATE TRIGGER trg_wallet_transactions_no_update
                BEFORE UPDATE ON wallet_transactions
                FOR EACH ROW
                EXECUTE FUNCTION prevent_wallet_transactions_mutation();
                """);

            migrationBuilder.Sql(
                """
                CREATE TRIGGER trg_wallet_transactions_no_delete
                BEFORE DELETE ON wallet_transactions
                FOR EACH ROW
                EXECUTE FUNCTION prevent_wallet_transactions_mutation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_wallet_transactions_no_update ON wallet_transactions;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_wallet_transactions_no_delete ON wallet_transactions;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS prevent_wallet_transactions_mutation();");
        }
    }
}
