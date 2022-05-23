using Microsoft.EntityFrameworkCore.Migrations;

namespace ShoppingListServer.Migrations
{
    public partial class AddListShareId : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ShareIdId",
                table: "ShoppingLists",
                type: "varchar(255)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingLists_ShareIdId",
                table: "ShoppingLists",
                column: "ShareIdId");

            migrationBuilder.AddForeignKey(
                name: "FK_ShoppingLists_ExpirationToken_ShareIdId",
                table: "ShoppingLists",
                column: "ShareIdId",
                principalTable: "ExpirationToken",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ShoppingLists_ExpirationToken_ShareIdId",
                table: "ShoppingLists");

            migrationBuilder.DropIndex(
                name: "IX_ShoppingLists_ShareIdId",
                table: "ShoppingLists");

            migrationBuilder.DropColumn(
                name: "ShareIdId",
                table: "ShoppingLists");
        }
    }
}
