using Microsoft.EntityFrameworkCore.Migrations;

namespace ShoppingListServer.Migrations
{
    public partial class AddOwnerToLists : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OwnerId",
                table: "ShoppingLists",
                type: "varchar(255)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingLists_OwnerId",
                table: "ShoppingLists",
                column: "OwnerId");

            migrationBuilder.AddForeignKey(
                name: "FK_ShoppingLists_Users_OwnerId",
                table: "ShoppingLists",
                column: "OwnerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ShoppingLists_Users_OwnerId",
                table: "ShoppingLists");

            migrationBuilder.DropIndex(
                name: "IX_ShoppingLists_OwnerId",
                table: "ShoppingLists");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "ShoppingLists");
        }
    }
}
