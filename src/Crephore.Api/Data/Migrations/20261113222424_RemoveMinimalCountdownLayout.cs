using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Crephore.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveMinimalCountdownLayout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Data steps (EF can't generate them; the schema doesn't change). The countdown's "minimal" layout (Minimal
            // numbers) was Full ("photo") without the date, so it was removed and Full gained a "Date" option (`showDate`).
            // Saving a website would fail on a section with an unknown layout, so saved ones move to Full looking as they
            // did: the date hidden, and their `textAlign` (start / center) as Full's `textPosition` (left / center). `Options`
            // on the right-hand side is the value before this update, so the position is read before `textAlign` goes.
            migrationBuilder.Sql("""
                UPDATE `website_sections`
                SET `Layout` = 'photo',
                    `Options` = JSON_SET(
                        JSON_REMOVE(`Options`, '$.textAlign'),
                        '$.textPosition', IF(JSON_UNQUOTE(JSON_EXTRACT(`Options`, '$.textAlign')) = 'start', 'left', 'center'),
                        '$.showDate', 'hide')
                WHERE `Type` = 'countdown' AND `Layout` = 'minimal';
                """);

            // Published copies are left as they are, unlike `RemoveFormalityLayout`'s: the dress code also has a "minimal"
            // layout, so replacing the text `"layout": "minimal"` would move those too. The web app reads a published
            // countdown "minimal" as Full with the same options (`currentLayoutKey`, `currentLayoutOptions`), so guests see
            // no change; the website reads "changes not published" until the host publishes again.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nothing to undo: which Full sections were Minimal numbers isn't kept
        }
    }
}
