using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OsuStocks.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketMaintenanceMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_maintenance_mode",
                table: "market_settings",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_maintenance_mode",
                table: "market_settings");
        }
    }
}
