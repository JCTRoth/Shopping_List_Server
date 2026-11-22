using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ShoppingListServer.Migrations
{
    /// <inheritdoc />
    public partial class StoreListContentInDatabase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "ShoppingLists",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DateString",
                table: "ShoppingLists",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "ShoppingLists",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "ShoppingLists",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "ShoppingLists",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ShoppingListProducts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ShoppingListId = table.Column<string>(type: "text", nullable: false),
                    ItemName = table.Column<string>(type: "text", nullable: false),
                    ItemImagePath = table.Column<string>(type: "text", nullable: true),
                    CategoryName = table.Column<string>(type: "text", nullable: true),
                    CategoryColorHex = table.Column<string>(type: "text", nullable: true),
                    CategoryImagePath = table.Column<string>(type: "text", nullable: true),
                    Count = table.Column<int>(type: "integer", nullable: false),
                    Checked = table.Column<bool>(type: "boolean", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShoppingListProducts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShoppingListProducts_ShoppingLists_ShoppingListId",
                        column: x => x.ShoppingListId,
                        principalTable: "ShoppingLists",
                        principalColumn: "SyncId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingListProducts_ShoppingListId_ItemName",
                table: "ShoppingListProducts",
                columns: new[] { "ShoppingListId", "ItemName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingListProducts_ShoppingListId_Position",
                table: "ShoppingListProducts",
                columns: new[] { "ShoppingListId", "Position" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShoppingListProducts");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "ShoppingLists");

            migrationBuilder.DropColumn(
                name: "DateString",
                table: "ShoppingLists");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "ShoppingLists");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "ShoppingLists");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "ShoppingLists");
        }
    }
}
