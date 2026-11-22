using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShoppingListServer.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExpirationToken",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Data = table.Column<string>(type: "text", nullable: true),
                    ExpirationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpirationToken", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ImageInfo",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    X = table.Column<int>(type: "integer", nullable: false),
                    Y = table.Column<int>(type: "integer", nullable: false),
                    Scale = table.Column<double>(type: "double precision", nullable: false),
                    Rotation = table.Column<double>(type: "double precision", nullable: false),
                    LastChangeTransformationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastChangeImageFileTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImageInfo", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    EMail = table.Column<string>(type: "text", nullable: true),
                    FirstName = table.Column<string>(type: "text", nullable: true),
                    LastName = table.Column<string>(type: "text", nullable: true),
                    Username = table.Column<string>(type: "text", nullable: true),
                    ExternalId = table.Column<string>(type: "text", nullable: true),
                    ColorArgb = table.Column<int>(type: "integer", nullable: true),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    Salt = table.Column<byte[]>(type: "bytea", maxLength: 16, nullable: true),
                    Role = table.Column<string>(type: "text", nullable: true),
                    Token = table.Column<string>(type: "text", nullable: true),
                    IsVerified = table.Column<bool>(type: "boolean", nullable: false),
                    ContactShareIdId = table.Column<string>(type: "text", nullable: true),
                    ProfilePictureInfoId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_ExpirationToken_ContactShareIdId",
                        column: x => x.ContactShareIdId,
                        principalTable: "ExpirationToken",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Users_ImageInfo_ProfilePictureInfoId",
                        column: x => x.ProfilePictureInfoId,
                        principalTable: "ImageInfo",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "EMailVerificationToken",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: true),
                    UrlCode = table.Column<string>(type: "text", nullable: true),
                    ExpirationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EMailVerificationToken", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EMailVerificationToken_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "FcmToken",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Token = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FcmToken", x => new { x.UserId, x.Token });
                    table.ForeignKey(
                        name: "FK_FcmToken_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PasswordAccess",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: true),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    Salt = table.Column<byte[]>(type: "bytea", maxLength: 16, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PasswordAccess", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PasswordAccess_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ResetPasswordToken",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: true),
                    Code = table.Column<string>(type: "text", nullable: true),
                    ExpirationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResetPasswordToken", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResetPasswordToken_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ShoppingLists",
                columns: table => new
                {
                    SyncId = table.Column<string>(type: "text", nullable: false),
                    LastChangeServerTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OwnerId = table.Column<string>(type: "text", nullable: true),
                    ShareIdId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShoppingLists", x => x.SyncId);
                    table.ForeignKey(
                        name: "FK_ShoppingLists_ExpirationToken_ShareIdId",
                        column: x => x.ShareIdId,
                        principalTable: "ExpirationToken",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ShoppingLists_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "UserContact",
                columns: table => new
                {
                    UserSourceId = table.Column<string>(type: "text", nullable: false),
                    UserTargetId = table.Column<string>(type: "text", nullable: false),
                    UserContactType = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserContact", x => new { x.UserSourceId, x.UserTargetId });
                    table.ForeignKey(
                        name: "FK_UserContact_Users_UserSourceId",
                        column: x => x.UserSourceId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserContact_Users_UserTargetId",
                        column: x => x.UserTargetId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ShoppingListPermission",
                columns: table => new
                {
                    ShoppingListId = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    PermissionType = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShoppingListPermission", x => new { x.ShoppingListId, x.UserId });
                    table.ForeignKey(
                        name: "FK_ShoppingListPermission_ShoppingLists_ShoppingListId",
                        column: x => x.ShoppingListId,
                        principalTable: "ShoppingLists",
                        principalColumn: "SyncId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ShoppingListPermission_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EMailVerificationToken_UserId",
                table: "EMailVerificationToken",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PasswordAccess_UserId",
                table: "PasswordAccess",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ResetPasswordToken_UserId",
                table: "ResetPasswordToken",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingListPermission_UserId",
                table: "ShoppingListPermission",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingLists_OwnerId",
                table: "ShoppingLists",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingLists_ShareIdId",
                table: "ShoppingLists",
                column: "ShareIdId");

            migrationBuilder.CreateIndex(
                name: "IX_UserContact_UserTargetId",
                table: "UserContact",
                column: "UserTargetId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_ContactShareIdId",
                table: "Users",
                column: "ContactShareIdId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_ProfilePictureInfoId",
                table: "Users",
                column: "ProfilePictureInfoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EMailVerificationToken");

            migrationBuilder.DropTable(
                name: "FcmToken");

            migrationBuilder.DropTable(
                name: "PasswordAccess");

            migrationBuilder.DropTable(
                name: "ResetPasswordToken");

            migrationBuilder.DropTable(
                name: "ShoppingListPermission");

            migrationBuilder.DropTable(
                name: "UserContact");

            migrationBuilder.DropTable(
                name: "ShoppingLists");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "ExpirationToken");

            migrationBuilder.DropTable(
                name: "ImageInfo");
        }
    }
}
