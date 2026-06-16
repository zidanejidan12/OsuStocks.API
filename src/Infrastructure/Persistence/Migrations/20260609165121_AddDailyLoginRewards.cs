using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OsuStocks.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyLoginRewards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "daily_reward_streak",
                table: "users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateOnly>(
                name: "last_daily_reward_date",
                table: "users",
                type: "date",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "daily_login_rewards",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reward_date = table.Column<DateOnly>(type: "date", nullable: false),
                    streak_day = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_daily_login_rewards", x => x.id);
                    table.ForeignKey(
                        name: "FK_daily_login_rewards_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "uq_daily_login_rewards_user_date",
                table: "daily_login_rewards",
                columns: new[] { "user_id", "reward_date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "daily_login_rewards");

            migrationBuilder.DropColumn(
                name: "daily_reward_streak",
                table: "users");

            migrationBuilder.DropColumn(
                name: "last_daily_reward_date",
                table: "users");
        }
    }
}
