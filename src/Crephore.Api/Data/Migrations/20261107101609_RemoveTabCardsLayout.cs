using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Crephore.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveTabCardsLayout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Data steps (EF can't generate them; the schema doesn't change). The program section's "tab-cards" layout
            // was removed, and saving a website would fail on a section with an unknown layout, so saved ones move to
            // "cards". Their options carry over: the ones cards doesn't have, or values it doesn't offer, read as its
            // defaults in the web app.
            migrationBuilder.Sql("UPDATE `website_sections` SET `Layout` = 'cards' WHERE `Type` = 'program' AND `Layout` = 'tab-cards';");

            // Published copies too, so a published website doesn't show "changes not published" for this alone. The copy
            // is a json column, which MySQL reads back as text in its own form (`"key": "value"`, a space after the
            // colon); quotes inside string values are escaped, so this exact text can only be a section's layout key.
            // (The web app also shows a published "tab-cards" as "cards".)
            migrationBuilder.Sql("""
                UPDATE `event_websites`
                SET `PublishedContent` = REPLACE(`PublishedContent`, '"layout": "tab-cards"', '"layout": "cards"')
                WHERE `PublishedContent` LIKE '%"layout": "tab-cards"%';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nothing to undo: which cards sections were tab cards isn't kept
        }
    }
}
