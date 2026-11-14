using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Crephore.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveHeroPhotoLayouts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Data steps (EF can't generate them; the schema doesn't change). The cover's Collage, Filmstrip, Framed print
            // and Offset frame layouts were removed; saving a website would fail on a section with an unknown layout, so
            // saved ones move to the closest cover, dropping the options it doesn't have: "collage" to "polaroid" (photos
            // beside the names; textPosition dropped), "filmstrip" to "horizon" (a wide photo above or below centred names;
            // textAlign dropped, photoPosition carries over), "framed" to "split-framed" (the photo beside the names;
            // textAlign dropped, and Framed print's photo side, left, written in where unset since Framed split's is right;
            // JSON_INSERT keeps a value that's there) and "offset-frame" to "split-framed" (the same options and defaults).
            migrationBuilder.Sql("""
                UPDATE `website_sections`
                SET `Layout` = 'polaroid',
                    `Options` = JSON_REMOVE(`Options`, '$.textPosition')
                WHERE `Type` = 'hero' AND `Layout` = 'collage';

                UPDATE `website_sections`
                SET `Layout` = 'horizon',
                    `Options` = JSON_REMOVE(`Options`, '$.textAlign')
                WHERE `Type` = 'hero' AND `Layout` = 'filmstrip';

                UPDATE `website_sections`
                SET `Layout` = 'split-framed',
                    `Options` = JSON_INSERT(JSON_REMOVE(COALESCE(`Options`, JSON_OBJECT()), '$.textAlign'), '$.photoSide', 'left')
                WHERE `Type` = 'hero' AND `Layout` = 'framed';

                UPDATE `website_sections`
                SET `Layout` = 'split-framed'
                WHERE `Type` = 'hero' AND `Layout` = 'offset-frame';
                """);

            // Published copies are left as they are: a text replace of the layout would keep the options the new layouts
            // don't have and lose Framed print's photo side. The web app shows a published cover's old layout as its
            // replacement with the same options (`currentLayoutKey`, `currentLayoutOptions`); the website reads "changes
            // not published" until the host publishes again.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nothing to undo: which covers were Collage, Filmstrip, Framed print or Offset frame isn't kept
        }
    }
}
