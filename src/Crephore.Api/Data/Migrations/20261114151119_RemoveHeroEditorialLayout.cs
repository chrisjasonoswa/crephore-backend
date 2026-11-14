using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Crephore.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveHeroEditorialLayout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Data steps (EF can't generate them; the schema doesn't change). The cover's "editorial" layout (Editorial,
            // which also holds the covers RemoveHeroBannerLayout moved from Banner) was replaced by the user's Editorial
            // designs (side-notes, photo-tab, masthead, horizon); saving a website would fail on a section with an unknown
            // layout, so saved ones move to "masthead", the closest (a wide rounded photo under left-aligned names). Its
            // option it no longer has (textAlign) goes; photoPosition and headingSize carry over.
            migrationBuilder.Sql("""
                UPDATE `website_sections`
                SET `Layout` = 'masthead',
                    `Options` = JSON_REMOVE(`Options`, '$.textAlign')
                WHERE `Type` = 'hero' AND `Layout` = 'editorial';
                """);

            // Published copies are left as they are: the message and the dress code also have an "editorial" layout, so
            // replacing the text `"layout": "editorial"` would move those too. The web app shows a published cover's
            // "editorial" as "masthead" (and a published "banner" as "horizon"; `currentLayoutKey`); the website reads
            // "changes not published" until the host publishes again.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nothing to undo: which Masthead covers were Editorial isn't kept
        }
    }
}
