using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShoppingListServer.Migrations
{
    /// <inheritdoc />
    public partial class FcmTokensPrimaryKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FcmToken_Users_UserId",
                table: "FcmToken");

            migrationBuilder.DropPrimaryKey(
                name: "PK_FcmToken",
                table: "FcmToken");

            migrationBuilder.DropIndex(
                name: "IX_FcmToken_UserId",
                table: "FcmToken");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "FcmToken");

            migrationBuilder.UpdateData(
                table: "FcmToken",
                keyColumn: "UserId",
                keyValue: null,
                column: "UserId",
                value: "");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "FcmToken",
                type: "varchar(255)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(255)",
                oldNullable: true);

            migrationBuilder.UpdateData(
                table: "FcmToken",
                keyColumn: "Token",
                keyValue: null,
                column: "Token",
                value: "");

            migrationBuilder.AlterColumn<string>(
                name: "Token",
                table: "FcmToken",
                type: "varchar(255)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_FcmToken",
                table: "FcmToken",
                columns: new[] { "UserId", "Token" });

            migrationBuilder.AddForeignKey(
                name: "FK_FcmToken_Users_UserId",
                table: "FcmToken",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FcmToken_Users_UserId",
                table: "FcmToken");

            migrationBuilder.DropPrimaryKey(
                name: "PK_FcmToken",
                table: "FcmToken");

            migrationBuilder.AlterColumn<string>(
                name: "Token",
                table: "FcmToken",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(255)");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "FcmToken",
                type: "varchar(255)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(255)");

            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "FcmToken",
                type: "int",
                nullable: false,
                defaultValue: 0)
                .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.AddPrimaryKey(
                name: "PK_FcmToken",
                table: "FcmToken",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_FcmToken_UserId",
                table: "FcmToken",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_FcmToken_Users_UserId",
                table: "FcmToken",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id");
        }
    }
}
