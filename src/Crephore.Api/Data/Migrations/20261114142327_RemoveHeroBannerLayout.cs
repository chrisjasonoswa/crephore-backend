using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Crephore.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveHeroBannerLayout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Data steps (EF can't generate them; the schema doesn't change). The cover's "banner" layout (Banner) was
            // removed; saving a website would fail on a section with an unknown layout, so saved ones move to "editorial",
            // the closest (one wide photo above or below the names, the same options). Banner's defaults differed
            // (centred text, the photo above; Editorial's are left and below), so they're written in where the host
            // never set them (JSON_INSERT keeps a value that's there), and the cover looks as it did.
            migrationBuilder.Sql("""
                UPDATE `website_sections`
                SET `Layout` = 'editorial',
                    `Options` = JSON_INSERT(`Options`, '$.textAlign', 'center', '$.photoPosition', 'above')
                WHERE `Type` = 'hero' AND `Layout` = 'banner';
                """);

            // Published copies are left as they are: replacing the text `"layout": "banner"` would lose Banner's defaults.
            // The web app shows a published cover's "banner" as "editorial" with them (`currentLayoutKey`,
            // `currentLayoutOptions`); the website reads "changes not published" until the host publishes again.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nothing to undo: which Editorial covers were Banner isn't kept
        }
    }
}
