using Microsoft.EntityFrameworkCore.Migrations;

namespace RiasBot.Core.Migrations
{
    public partial class Translations : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Locale",
                table: "Guilds",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Locale",
                table: "Guilds");
        }
    }
}
