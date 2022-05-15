using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ShoppingListServer.Migrations
{
    public partial class AddContactShareId : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContactShareIdId",
                table: "Users",
                type: "varchar(255)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ExpirationToken",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(255)", nullable: false),
                    Data = table.Column<string>(type: "longtext", nullable: true),
                    ExpirationTime = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpirationToken", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_ContactShareIdId",
                table: "Users",
                column: "ContactShareIdId");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_ExpirationToken_ContactShareIdId",
                table: "Users",
                column: "ContactShareIdId",
                principalTable: "ExpirationToken",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_ExpirationToken_ContactShareIdId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "ExpirationToken");

            migrationBuilder.DropIndex(
                name: "IX_Users_ContactShareIdId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ContactShareIdId",
                table: "Users");
        }
    }
}
