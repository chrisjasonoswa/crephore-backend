using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Crephore.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveHeroArchLayout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Data steps (EF can't generate them; the schema doesn't change). The cover's "arch" layout (Arch frame) was
            // replaced by two arch covers (arch-cards, arch-center); saving a website would fail on a section with an
            // unknown layout, so saved ones move to "arch-center", the closest (the photo in the middle). Its option it no
            // longer has (namesPlacement) goes; headingSize carries over.
            migrationBuilder.Sql("""
                UPDATE `website_sections`
                SET `Layout` = 'arch-center',
                    `Options` = JSON_REMOVE(`Options`, '$.namesPlacement')
                WHERE `Type` = 'hero' AND `Layout` = 'arch';
                """);

            // Published copies too, so a published website doesn't show "changes not published" for this alone. Only the
            // cover has an "arch" layout (other types' "arch" is an option value, never under "layout"), and quotes inside
            // string values are escaped, so this exact text can only be a cover's layout key. (The web app also shows a
            // published "arch" as "arch-center" and ignores its old option.)
            migrationBuilder.Sql("""
                UPDATE `event_websites`
                SET `PublishedContent` = REPLACE(`PublishedContent`, '"layout": "arch"', '"layout": "arch-center"')
                WHERE `PublishedContent` LIKE '%"layout": "arch"%';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nothing to undo: which Arch centre covers were Arch frame isn't kept
        }
    }
}
