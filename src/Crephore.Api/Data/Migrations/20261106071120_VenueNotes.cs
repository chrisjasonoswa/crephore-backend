using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Crephore.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class VenueNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "event_venues",
                type: "varchar(1000)",
                maxLength: 1000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            // Data steps (EF can't generate them), after Notes exists and before the old columns are dropped (EF
            // generated the drops first; they were moved below).

            // 1. A venue's directions and parking become its notes: each trimmed, the blank ones skipped, joined by a
            //    newline (CHAR(10), which doesn't depend on the server's backslash-escape mode); null when both are blank.
            //    Cut to the column's 1000 characters: only both at their full 500 (1001 with the newline) lose their last
            //    one. Label is dropped without a copy (it holds nothing to keep).
            migrationBuilder.Sql("""
                UPDATE `event_venues`
                SET `Notes` = LEFT(NULLIF(CONCAT_WS(CHAR(10 USING utf8mb4), NULLIF(TRIM(`Directions`), ''), NULLIF(TRIM(`Parking`), '')), ''), 1000);
                """);

            // 2. The "venue" section type no longer exists (venue details show in the program sections), and saving a
            //    website would fail on a section of an unknown type, so saved ones are deleted. Published copies
            //    (event_websites.PublishedContent) are left as they are: the web app skips section types it doesn't know.
            migrationBuilder.Sql("DELETE FROM `website_sections` WHERE `Type` = 'venue';");

            migrationBuilder.DropColumn(
                name: "Directions",
                table: "event_venues");

            migrationBuilder.DropColumn(
                name: "Label",
                table: "event_venues");

            migrationBuilder.DropColumn(
                name: "Parking",
                table: "event_venues");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Directions",
                table: "event_venues",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Label",
                table: "event_venues",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Parking",
                table: "event_venues",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            // Data step (EF can't generate it), after the old columns are back and before Notes is dropped (EF
            // generated the drop first; it was moved below). Lossy: the notes go into Directions whole, cut to its 500
            // characters, and are not split back into directions and parking; labels stay empty; deleted "venue"
            // sections are not restored. The earlier model has no Notes property, so UpdateData can't be used.
            migrationBuilder.Sql("UPDATE `event_venues` SET `Directions` = LEFT(`Notes`, 500) WHERE `Notes` IS NOT NULL;");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "event_venues");
        }
    }
}
