using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Crephore.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class HeadingSizes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HeadingSize",
                table: "event_websites",
                type: "varchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "medium")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "HeroHeadingSize",
                table: "event_websites",
                type: "varchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "medium")
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HeadingSize",
                table: "event_websites");

            migrationBuilder.DropColumn(
                name: "HeroHeadingSize",
                table: "event_websites");
        }
    }
}
