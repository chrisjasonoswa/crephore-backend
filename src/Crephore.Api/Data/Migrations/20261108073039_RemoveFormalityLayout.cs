using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Crephore.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveFormalityLayout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Data steps (EF can't generate them; the schema doesn't change). The dress code's "formality" layout (the
            // formality scale) was removed with the attire presets it placed on the scale, and saving a website would
            // fail on a section with an unknown layout, so saved ones move to "palette", which it otherwise matched.
            // Their options read as the palette's defaults in the web app.
            migrationBuilder.Sql("UPDATE `website_sections` SET `Layout` = 'palette' WHERE `Type` = 'dress-code' AND `Layout` = 'formality';");

            // Published copies too, so a published website doesn't show "changes not published" for this alone. The copy
            // is a json column, which MySQL reads back as text in its own form (`"key": "value"`, a space after the
            // colon); quotes inside string values are escaped, so this exact text can only be a section's layout key.
            // (The web app also shows a published "formality" as "palette".)
            migrationBuilder.Sql("""
                UPDATE `event_websites`
                SET `PublishedContent` = REPLACE(`PublishedContent`, '"layout": "formality"', '"layout": "palette"')
                WHERE `PublishedContent` LIKE '%"layout": "formality"%';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nothing to undo: which palette sections were formality scales isn't kept
        }
    }
}
