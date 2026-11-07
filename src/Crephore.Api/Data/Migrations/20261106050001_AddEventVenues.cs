using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Crephore.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEventVenues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "VenueId",
                table: "event_program_items",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "event_venues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Address = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    GoogleMapsUrl = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AppleMapsUrl = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Latitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    Longitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    Label = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PhotoUrl = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Directions = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Parking = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    EventId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_venues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_event_venues_events_EventId",
                        column: x => x.EventId,
                        principalTable: "events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_event_program_items_VenueId",
                table: "event_program_items",
                column: "VenueId");

            migrationBuilder.CreateIndex(
                name: "IX_event_venues_EventId_SortOrder",
                table: "event_venues",
                columns: new[] { "EventId", "SortOrder" });

            migrationBuilder.AddForeignKey(
                name: "FK_event_program_items_event_venues_VenueId",
                table: "event_program_items",
                column: "VenueId",
                principalTable: "event_venues",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // Data steps (EF can't generate them): the event's venue and the venues its program items named become
            // rows of event_venues, and the items point to them. They run after the new table and column exist and
            // before the old columns are dropped (EF generated the drops first; they were moved below).

            // 1. Each event's own venue becomes its main venue (SortOrder 0). The address is cut to the new column's 300
            //    characters (it was 500), and the pin is kept only when both values are set (now both or neither).
            migrationBuilder.Sql("""
                INSERT INTO `event_venues` (`EventId`, `SortOrder`, `Name`, `Address`, `Latitude`, `Longitude`, `CreatedAt`, `UpdatedAt`)
                SELECT e.`Id`, 0, TRIM(e.`VenueName`), LEFT(NULLIF(TRIM(e.`VenueAddress`), ''), 300),
                    CASE WHEN e.`VenueLatitude` IS NOT NULL AND e.`VenueLongitude` IS NOT NULL THEN e.`VenueLatitude` END,
                    CASE WHEN e.`VenueLatitude` IS NOT NULL AND e.`VenueLongitude` IS NOT NULL THEN e.`VenueLongitude` END,
                    UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
                FROM `events` e
                WHERE TRIM(COALESCE(e.`VenueName`, '')) <> '';
                """);

            // 2. Each other venue an event's items named (trimmed, case-insensitive; not the event's own) becomes another
            //    venue of that event, in order of first appearance (date, start time, position), right after the main
            //    venue: SortOrder 1, 2… (0, 1… for an event without a venue of its own, so its first is the main one).
            migrationBuilder.Sql("""
                INSERT INTO `event_venues` (`EventId`, `SortOrder`, `Name`, `CreatedAt`, `UpdatedAt`)
                SELECT f.`EventId`,
                    ROW_NUMBER() OVER (PARTITION BY f.`EventId` ORDER BY f.`Date`, f.`StartTime`, f.`SortOrder`, f.`Id`) - f.`NoMainVenue`,
                    f.`Name`, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
                FROM (
                    SELECT i.`EventId`, TRIM(i.`VenueName`) AS `Name`, i.`Date`, i.`StartTime`, i.`SortOrder`, i.`Id`,
                        CASE WHEN TRIM(COALESCE(e.`VenueName`, '')) = '' THEN 1 ELSE 0 END AS `NoMainVenue`,
                        ROW_NUMBER() OVER (
                            PARTITION BY i.`EventId`, LOWER(TRIM(i.`VenueName`))
                            ORDER BY i.`Date`, i.`StartTime`, i.`SortOrder`, i.`Id`) AS `Occurrence`
                    FROM `event_program_items` i
                    JOIN `events` e ON e.`Id` = i.`EventId`
                    WHERE TRIM(COALESCE(i.`VenueName`, '')) <> ''
                        AND LOWER(TRIM(i.`VenueName`)) <> LOWER(TRIM(COALESCE(e.`VenueName`, '')))
                ) f
                WHERE f.`Occurrence` = 1;
                """);

            // 3. Items that named a venue point to that venue of their event (the main one when it's the event's own).
            migrationBuilder.Sql("""
                UPDATE `event_program_items` i
                JOIN `event_venues` v ON v.`EventId` = i.`EventId` AND LOWER(v.`Name`) = LOWER(TRIM(i.`VenueName`))
                SET i.`VenueId` = v.`Id`
                WHERE TRIM(COALESCE(i.`VenueName`, '')) <> '';
                """);

            // 4. Items without one were at the event's venue: they point to its main venue, when the event had a venue
            //    of its own (otherwise they stay without one).
            migrationBuilder.Sql("""
                UPDATE `event_program_items` i
                JOIN `events` e ON e.`Id` = i.`EventId`
                JOIN `event_venues` v ON v.`EventId` = i.`EventId` AND v.`SortOrder` = 0
                SET i.`VenueId` = v.`Id`
                WHERE TRIM(COALESCE(i.`VenueName`, '')) = '' AND TRIM(COALESCE(e.`VenueName`, '')) <> '';
                """);

            migrationBuilder.DropColumn(
                name: "ProgramAtEventVenue",
                table: "events");

            migrationBuilder.DropColumn(
                name: "VenueAddress",
                table: "events");

            migrationBuilder.DropColumn(
                name: "VenueGooglePlaceId",
                table: "events");

            migrationBuilder.DropColumn(
                name: "VenueLatitude",
                table: "events");

            migrationBuilder.DropColumn(
                name: "VenueLongitude",
                table: "events");

            migrationBuilder.DropColumn(
                name: "VenueName",
                table: "events");

            migrationBuilder.DropColumn(
                name: "VenueName",
                table: "event_program_items");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ProgramAtEventVenue",
                table: "events",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "VenueAddress",
                table: "events",
                type: "varchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "VenueGooglePlaceId",
                table: "events",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "VenueLatitude",
                table: "events",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "VenueLongitude",
                table: "events",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VenueName",
                table: "events",
                type: "varchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "VenueName",
                table: "event_program_items",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            // Data steps (EF can't generate them), before the venues are dropped (EF generated the drops first; they
            // were moved below). Lossy: only the main venue's name, address and pin go back onto the event (its other
            // venues and every venue's links, label, photo, directions and parking are lost), items get their venue's
            // name, and every program is at its event's venue again (ProgramAtEventVenue's default, true). The earlier
            // model has none of these columns, so UpdateData can't be used.
            migrationBuilder.Sql("""
                UPDATE `events` e
                JOIN (
                    SELECT `EventId`, `Name`, `Address`, `Latitude`, `Longitude`,
                        ROW_NUMBER() OVER (PARTITION BY `EventId` ORDER BY `SortOrder`, `Id`) AS `Position`
                    FROM `event_venues`
                ) v ON v.`EventId` = e.`Id` AND v.`Position` = 1
                SET e.`VenueName` = v.`Name`, e.`VenueAddress` = COALESCE(v.`Address`, ''),
                    e.`VenueLatitude` = v.`Latitude`, e.`VenueLongitude` = v.`Longitude`;
                """);

            migrationBuilder.Sql("""
                UPDATE `event_program_items` i
                JOIN `event_venues` v ON v.`Id` = i.`VenueId`
                SET i.`VenueName` = v.`Name`;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_event_program_items_event_venues_VenueId",
                table: "event_program_items");

            migrationBuilder.DropTable(
                name: "event_venues");

            migrationBuilder.DropIndex(
                name: "IX_event_program_items_VenueId",
                table: "event_program_items");

            migrationBuilder.DropColumn(
                name: "VenueId",
                table: "event_program_items");
        }
    }
}
