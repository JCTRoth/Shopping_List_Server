using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShoppingListServer.Migrations
{
    public partial class AddImageInfo : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProfilePictureInfoId",
                table: "Users",
                type: "varchar(255)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ImageInfo",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(255)", nullable: false),
                    X = table.Column<int>(type: "int", nullable: false),
                    Y = table.Column<int>(type: "int", nullable: false),
                    Scale = table.Column<double>(type: "double", nullable: false),
                    Rotation = table.Column<double>(type: "double", nullable: false),
                    LastChangeTransformationTime = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastChangeImageFileTime = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImageInfo", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_ProfilePictureInfoId",
                table: "Users",
                column: "ProfilePictureInfoId");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_ImageInfo_ProfilePictureInfoId",
                table: "Users",
                column: "ProfilePictureInfoId",
                principalTable: "ImageInfo",
                principalColumn: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_ImageInfo_ProfilePictureInfoId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "ImageInfo");

            migrationBuilder.DropIndex(
                name: "IX_Users_ProfilePictureInfoId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ProfilePictureInfoId",
                table: "Users");
        }
    }
}
