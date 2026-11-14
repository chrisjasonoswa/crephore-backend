using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Crephore.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveEyebrowCase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EyebrowCase",
                table: "event_websites");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EyebrowCase",
                table: "event_websites",
                type: "varchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "uppercase")
                .Annotation("MySql:CharSet", "utf8mb4");
        }
    }
}
