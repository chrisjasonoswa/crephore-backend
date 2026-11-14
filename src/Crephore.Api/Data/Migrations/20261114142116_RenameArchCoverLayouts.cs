using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Crephore.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameArchCoverLayouts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Data steps (EF can't generate them; the schema doesn't change). The cover's "arch-cards" and "arch-center"
            // layouts (added by RemoveHeroArchLayout's change) were renamed "detail-cards" and "centre-stage" once their
            // photo could be a box as well as an arch. Saving a website would fail on a section with an unknown layout,
            // so saved ones move; their options are the same.
            migrationBuilder.Sql("""
                UPDATE `website_sections`
                SET `Layout` = CASE `Layout` WHEN 'arch-cards' THEN 'detail-cards' ELSE 'centre-stage' END
                WHERE `Type` = 'hero' AND `Layout` IN ('arch-cards', 'arch-center');
                """);

            // Published copies too, so a published website doesn't show "changes not published" for this alone. Only the
            // cover has these layout keys, and quotes inside string values are escaped, so this exact text can only be a
            // cover's layout key. (The web app also shows the old keys as the new ones.)
            migrationBuilder.Sql("""
                UPDATE `event_websites`
                SET `PublishedContent` = REPLACE(REPLACE(`PublishedContent`,
                        '"layout": "arch-cards"', '"layout": "detail-cards"'),
                        '"layout": "arch-center"', '"layout": "centre-stage"')
                WHERE `PublishedContent` LIKE '%"layout": "arch-cards"%' OR `PublishedContent` LIKE '%"layout": "arch-center"%';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Back to the earlier keys
            migrationBuilder.Sql("""
                UPDATE `website_sections`
                SET `Layout` = CASE `Layout` WHEN 'detail-cards' THEN 'arch-cards' ELSE 'arch-center' END
                WHERE `Type` = 'hero' AND `Layout` IN ('detail-cards', 'centre-stage');
                """);
            migrationBuilder.Sql("""
                UPDATE `event_websites`
                SET `PublishedContent` = REPLACE(REPLACE(`PublishedContent`,
                        '"layout": "detail-cards"', '"layout": "arch-cards"'),
                        '"layout": "centre-stage"', '"layout": "arch-center"')
                WHERE `PublishedContent` LIKE '%"layout": "detail-cards"%' OR `PublishedContent` LIKE '%"layout": "centre-stage"%';
                """);
        }
    }
}
