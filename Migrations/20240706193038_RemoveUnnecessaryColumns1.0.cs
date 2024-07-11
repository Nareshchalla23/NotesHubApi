using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NotesHubApi.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUnnecessaryColumns10 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GitHubLogins",
                columns: table => new
                {
                    LoginId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())"),
                    GitHubID = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Username = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    AvatarUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    AccessToken = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(getdate())"),
                    LastLoginDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(getdate())"),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClientId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GitHubLogin", x => x.LoginId);
                });

            migrationBuilder.CreateTable(
                name: "GoogleLogins",
                columns: table => new
                {
                    LoginId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())"),
                    ClientId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Credential = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    SelectBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Picture = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    Sub = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(getdate())"),
                    LastLoginDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoogleLogin", x => x.LoginId);
                });

            migrationBuilder.CreateTable(
                name: "JWTLogins",
                columns: table => new
                {
                    LoginId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())"),
                    Username = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "(getdate())"),
                    LastLoginDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: true, defaultValue: true),
                    ProfilePictureUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Jwtlogin", x => x.LoginId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GitHubLogins_Email",
                table: "GitHubLogins",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GitHubLogins_GitHubID",
                table: "GitHubLogins",
                column: "GitHubID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GoogleLogins_Email",
                table: "GoogleLogins",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GoogleLogins_Sub",
                table: "GoogleLogins",
                column: "Sub",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JWTLogins_Email",
                table: "JWTLogins",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GitHubLogins");

            migrationBuilder.DropTable(
                name: "GoogleLogins");

            migrationBuilder.DropTable(
                name: "JWTLogins");
        }
    }
}
