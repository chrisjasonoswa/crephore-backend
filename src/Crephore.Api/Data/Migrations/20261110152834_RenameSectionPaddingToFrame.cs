using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Crephore.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameSectionPaddingToFrame : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Data steps (EF can't generate them; the schema doesn't change). The cover's "Section padding" option
            // (`sectionPadding`: none / padded) became "Frame" (`frame`: none / inset), so it can gain decorative frames.
            // Saved sections move to the new key and value; a section that somehow has both keeps its `frame`.
            migrationBuilder.Sql("""
                UPDATE `website_sections`
                SET `Options` = JSON_REMOVE(
                    IF(JSON_CONTAINS_PATH(`Options`, 'one', '$.frame'), `Options`,
                        JSON_SET(`Options`, '$.frame',
                            IF(JSON_UNQUOTE(JSON_EXTRACT(`Options`, '$.sectionPadding')) = 'padded', 'inset',
                                JSON_UNQUOTE(JSON_EXTRACT(`Options`, '$.sectionPadding'))))),
                    '$.sectionPadding')
                WHERE `Type` = 'hero' AND JSON_CONTAINS_PATH(`Options`, 'one', '$.sectionPadding');
                """);

            // Published copies too, so a published website doesn't show "changes not published" for this alone. The copy
            // is a json column, which MySQL reads back as text in its own form (`"key": "value"`, a space after the
            // colon); quotes inside string values are escaped, so this exact text can only be the option. (The web app
            // also reads a published `sectionPadding` as `frame`.)
            migrationBuilder.Sql("""
                UPDATE `event_websites`
                SET `PublishedContent` = REPLACE(REPLACE(`PublishedContent`,
                    '"sectionPadding": "padded"', '"frame": "inset"'),
                    '"sectionPadding": "none"', '"frame": "none"')
                WHERE `PublishedContent` LIKE '%"sectionPadding": %';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE `website_sections`
                SET `Options` = JSON_REMOVE(
                    JSON_SET(`Options`, '$.sectionPadding',
                        IF(JSON_UNQUOTE(JSON_EXTRACT(`Options`, '$.frame')) = 'inset', 'padded',
                            JSON_UNQUOTE(JSON_EXTRACT(`Options`, '$.frame')))),
                    '$.frame')
                WHERE `Type` = 'hero' AND JSON_CONTAINS_PATH(`Options`, 'one', '$.frame');
                """);
            migrationBuilder.Sql("""
                UPDATE `event_websites`
                SET `PublishedContent` = REPLACE(REPLACE(`PublishedContent`,
                    '"frame": "inset"', '"sectionPadding": "padded"'),
                    '"frame": "none"', '"sectionPadding": "none"')
                WHERE `PublishedContent` LIKE '%"frame": %';
                """);
        }
    }
}
