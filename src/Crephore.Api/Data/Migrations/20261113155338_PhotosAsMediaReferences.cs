using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Crephore.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class PhotosAsMediaReferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PhotoMediaId",
                table: "event_venues",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_event_venues_PhotoMediaId",
                table: "event_venues",
                column: "PhotoMediaId");

            migrationBuilder.AddForeignKey(
                name: "FK_event_venues_media_PhotoMediaId",
                table: "event_venues",
                column: "PhotoMediaId",
                principalTable: "media",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // Added by hand (EF can't generate data steps): photos were stored as URLs, which end with the media's
            // storage key ("http://localhost:5180/uploads/events/10/<guid>.jpg"). They become references to that media
            // of the same event. Before the generated drop of PhotoUrl, which this reads.
            migrationBuilder.Sql(
                """
                UPDATE event_venues v
                JOIN media m ON m.EventId = v.EventId AND v.PhotoUrl LIKE CONCAT('%/', m.StorageKey)
                SET v.PhotoMediaId = m.Id;
                """);

            migrationBuilder.DropColumn(
                name: "PhotoUrl",
                table: "event_venues");

            // Every URL string in the sections' content and the published copies becomes { "mediaId": … }. A row can
            // hold several photos and a joined UPDATE changes each row once, so it repeats until nothing changes.
            migrationBuilder.Sql(
                """
                CREATE PROCEDURE crephore_photos_as_media_references()
                BEGIN
                    DECLARE changed INT DEFAULT 1;
                    WHILE changed > 0 DO
                        UPDATE website_sections s
                        JOIN event_websites w ON w.Id = s.EventWebsiteId
                        JOIN media m ON m.EventId = w.EventId
                            AND JSON_SEARCH(s.Content, 'one', CONCAT('%/', m.StorageKey)) IS NOT NULL
                        SET s.Content = REGEXP_REPLACE(CAST(s.Content AS CHAR),
                            CONCAT('"[^"]*/', REPLACE(m.StorageKey, '.', '[.]'), '"'),
                            CONCAT('{"mediaId": ', m.Id, '}'));
                        SET changed = ROW_COUNT();

                        UPDATE event_websites w
                        JOIN media m ON m.EventId = w.EventId
                            AND w.PublishedContent IS NOT NULL
                            AND JSON_SEARCH(w.PublishedContent, 'one', CONCAT('%/', m.StorageKey)) IS NOT NULL
                        SET w.PublishedContent = REGEXP_REPLACE(CAST(w.PublishedContent AS CHAR),
                            CONCAT('"[^"]*/', REPLACE(m.StorageKey, '.', '[.]'), '"'),
                            CONCAT('{"mediaId": ', m.Id, '}'));
                        SET changed = changed + ROW_COUNT();
                    END WHILE;
                END
                """);
            migrationBuilder.Sql("CALL crephore_photos_as_media_references();");
            migrationBuilder.Sql("DROP PROCEDURE crephore_photos_as_media_references;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Photos aren't turned back into URLs: those depended on where the API served files from. Venues lose
            // their photo and sections keep their references, which the earlier web app doesn't show.
            migrationBuilder.DropForeignKey(
                name: "FK_event_venues_media_PhotoMediaId",
                table: "event_venues");

            migrationBuilder.DropIndex(
                name: "IX_event_venues_PhotoMediaId",
                table: "event_venues");

            migrationBuilder.DropColumn(
                name: "PhotoMediaId",
                table: "event_venues");

            migrationBuilder.AddColumn<string>(
                name: "PhotoUrl",
                table: "event_venues",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }
    }
}
