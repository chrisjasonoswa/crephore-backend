using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Crephore.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEyebrowStyleAndDaysCountdownLayouts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EyebrowCase",
                table: "event_websites",
                type: "varchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "uppercase")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "EyebrowStyle",
                table: "event_websites",
                type: "varchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "plain")
                .Annotation("MySql:CharSet", "utf8mb4");

            // Data steps (EF can't generate them). The countdown's "days-only" layout was replaced by four days layouts
            // (days-inline, days-stacked, days-editorial, days-outline); saving a website would fail on a section with an
            // unknown layout, so saved ones move to "days-stacked", the closest (one big number with "days to go"). Their
            // options read as its defaults in the web app.
            migrationBuilder.Sql("UPDATE `website_sections` SET `Layout` = 'days-stacked' WHERE `Type` = 'countdown' AND `Layout` = 'days-only';");

            // Published copies too, so a published website doesn't show "changes not published" for this alone. Only the
            // countdown has a layout called "days-only", and quotes inside string values are escaped in MySQL's text form
            // of the json column, so this exact text can only be that layout key. (The web app also shows a published
            // "days-only" as "days-stacked".)
            migrationBuilder.Sql("""
                UPDATE `event_websites`
                SET `PublishedContent` = REPLACE(`PublishedContent`, '"layout": "days-only"', '"layout": "days-stacked"')
                WHERE `PublishedContent` LIKE '%"layout": "days-only"%';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Which days-stacked sections were days-only isn't kept, so they stay as they are

            migrationBuilder.DropColumn(
                name: "EyebrowCase",
                table: "event_websites");

            migrationBuilder.DropColumn(
                name: "EyebrowStyle",
                table: "event_websites");
        }
    }
}
