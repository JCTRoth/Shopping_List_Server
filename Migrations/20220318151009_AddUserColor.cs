using Microsoft.EntityFrameworkCore.Migrations;

namespace ShoppingListServer.Migrations
{
    public partial class AddUserColor : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ColorArgb",
                table: "Users",
                type: "int",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ColorArgb",
                table: "Users");
        }
    }
}
