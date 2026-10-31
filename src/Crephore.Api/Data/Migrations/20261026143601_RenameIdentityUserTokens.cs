using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Crephore.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameIdentityUserTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_app_user_tokens_app_users_UserId",
                table: "app_user_tokens");

            migrationBuilder.DropTable(
                name: "app_user_sessions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_app_user_tokens",
                table: "app_user_tokens");

            migrationBuilder.RenameTable(
                name: "app_user_tokens",
                newName: "app_user_identity_tokens");

            migrationBuilder.AddPrimaryKey(
                name: "PK_app_user_identity_tokens",
                table: "app_user_identity_tokens",
                columns: new[] { "UserId", "LoginProvider", "Name" });

            migrationBuilder.AddForeignKey(
                name: "FK_app_user_identity_tokens_app_users_UserId",
                table: "app_user_identity_tokens",
                column: "UserId",
                principalTable: "app_users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_app_user_identity_tokens_app_users_UserId",
                table: "app_user_identity_tokens");

            migrationBuilder.DropPrimaryKey(
                name: "PK_app_user_identity_tokens",
                table: "app_user_identity_tokens");

            migrationBuilder.RenameTable(
                name: "app_user_identity_tokens",
                newName: "app_user_tokens");

            migrationBuilder.AddPrimaryKey(
                name: "PK_app_user_tokens",
                table: "app_user_tokens",
                columns: new[] { "UserId", "LoginProvider", "Name" });

            migrationBuilder.CreateTable(
                name: "app_user_sessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    CreatedByIp = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    RevokedByIp = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SessionId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    UserAgent = table.Column<string>(type: "varchar(512)", maxLength: 512, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_app_user_sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_app_user_sessions_app_users_UserId",
                        column: x => x.UserId,
                        principalTable: "app_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_app_user_sessions_SessionId",
                table: "app_user_sessions",
                column: "SessionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_app_user_sessions_UserId_SessionId",
                table: "app_user_sessions",
                columns: new[] { "UserId", "SessionId" });

            migrationBuilder.AddForeignKey(
                name: "FK_app_user_tokens_app_users_UserId",
                table: "app_user_tokens",
                column: "UserId",
                principalTable: "app_users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
