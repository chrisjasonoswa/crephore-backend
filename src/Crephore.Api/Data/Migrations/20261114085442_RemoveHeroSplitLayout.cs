using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Crephore.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveHeroSplitLayout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Data steps (EF can't generate them; the schema doesn't change). The cover's "split" layout was replaced by
            // four split covers (split-framed, split-overlap, split-bento, split-index); saving a website would fail on a
            // section with an unknown layout, so saved ones move to "split-framed", the closest (the photo on its own
            // side). Its options it no longer has (textPosition, photoFrame) go; photoSide and headingSize carry over.
            migrationBuilder.Sql("""
                UPDATE `website_sections`
                SET `Layout` = 'split-framed',
                    `Options` = JSON_REMOVE(`Options`, '$.textPosition', '$.photoFrame')
                WHERE `Type` = 'hero' AND `Layout` = 'split';
                """);

            // Published copies are left as they are: other section types (the countdown, the message, …) also have a
            // "split" layout, so replacing the text `"layout": "split"` would move those too. The web app shows a
            // published cover's "split" as "split-framed" (`currentLayoutKey`); the website reads "changes not
            // published" until the host publishes again.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nothing to undo: which Framed split covers were Split isn't kept
        }
    }
}
