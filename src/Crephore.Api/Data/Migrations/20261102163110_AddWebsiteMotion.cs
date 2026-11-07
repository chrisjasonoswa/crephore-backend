using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Crephore.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWebsiteMotion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MotionAnimation",
                table: "event_websites",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "fade-up")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "MotionIntensity",
                table: "event_websites",
                type: "varchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "normal")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "MotionTransition",
                table: "event_websites",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "fade")
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MotionAnimation",
                table: "event_websites");

            migrationBuilder.DropColumn(
                name: "MotionIntensity",
                table: "event_websites");

            migrationBuilder.DropColumn(
                name: "MotionTransition",
                table: "event_websites");
        }
    }
}
