using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OsuStocks.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProfileCoverUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "profile_cover_url",
                table: "users",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "profile_cover_url",
                table: "tracked_players",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "profile_cover_url",
                table: "users");

            migrationBuilder.DropColumn(
                name: "profile_cover_url",
                table: "tracked_players");
        }
    }
}
