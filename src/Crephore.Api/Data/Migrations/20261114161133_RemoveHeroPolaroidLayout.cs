using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Crephore.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveHeroPolaroidLayout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Data steps (EF can't generate them; the schema doesn't change). The cover's "polaroid" layout (Polaroids,
            // which also holds the covers RemoveHeroPhotoLayouts moved from Collage) was replaced by the user's polaroid
            // designs (polaroid-note, polaroid-row, polaroid-stack); saving a website would fail on a section with an
            // unknown layout, so saved ones move to "polaroid-note", the closest (tilted prints under the names). Its
            // option it no longer has (photoSide) goes; headingSize carries over.
            migrationBuilder.Sql("""
                UPDATE `website_sections`
                SET `Layout` = 'polaroid-note',
                    `Options` = JSON_REMOVE(`Options`, '$.photoSide')
                WHERE `Type` = 'hero' AND `Layout` = 'polaroid';
                """);

            // Published copies are left as they are: the message also has a "polaroid" layout, so replacing the text
            // `"layout": "polaroid"` would move those too. The web app shows a published cover's "polaroid" (and
            // "collage") as "polaroid-note" (`currentLayoutKey`); the website reads "changes not published" until the
            // host publishes again.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nothing to undo: which Photo & note covers were Polaroids isn't kept
        }
    }
}
