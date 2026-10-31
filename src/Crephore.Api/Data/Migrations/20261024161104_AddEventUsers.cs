using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Crephore.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEventUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_events_app_users_OwnerId",
                table: "events");

            migrationBuilder.DropForeignKey(
                name: "FK_events_event_groups_EventGroupId",
                table: "events");

            migrationBuilder.DropIndex(
                name: "IX_events_EventGroupId",
                table: "events");

            migrationBuilder.DropIndex(
                name: "IX_events_OwnerId",
                table: "events");

            migrationBuilder.DropColumn(
                name: "EventGroupId",
                table: "events");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "events");

            migrationBuilder.CreateTable(
                name: "event_users",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    EventId = table.Column<int>(type: "int", nullable: false),
                    Access = table.Column<string>(type: "json", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    EventGroupId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_users", x => new { x.UserId, x.EventId });
                    table.ForeignKey(
                        name: "FK_event_users_app_users_UserId",
                        column: x => x.UserId,
                        principalTable: "app_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_event_users_event_groups_EventGroupId",
                        column: x => x.EventGroupId,
                        principalTable: "event_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_event_users_events_EventId",
                        column: x => x.EventId,
                        principalTable: "events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_event_users_EventGroupId",
                table: "event_users",
                column: "EventGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_event_users_EventId",
                table: "event_users",
                column: "EventId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "event_users");

            migrationBuilder.AddColumn<int>(
                name: "EventGroupId",
                table: "events",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OwnerId",
                table: "events",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_events_EventGroupId",
                table: "events",
                column: "EventGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_events_OwnerId",
                table: "events",
                column: "OwnerId");

            migrationBuilder.AddForeignKey(
                name: "FK_events_app_users_OwnerId",
                table: "events",
                column: "OwnerId",
                principalTable: "app_users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_events_event_groups_EventGroupId",
                table: "events",
                column: "EventGroupId",
                principalTable: "event_groups",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
