using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Crephore.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class SecondaryAndAccentColors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // EF generates this as altering AccentColor and adding SecondaryColor; today's accent colour is the new
            // secondary colour, so the column is renamed instead, and sections on the "accent" background move to
            // "secondary" (the key was renamed). Both colours are now optional.
            migrationBuilder.RenameColumn(
                name: "AccentColor",
                table: "event_websites",
                newName: "SecondaryColor");

            migrationBuilder.AlterColumn<string>(
                name: "SecondaryColor",
                table: "event_websites",
                type: "varchar(7)",
                maxLength: 7,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(7)",
                oldMaxLength: 7)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "AccentColor",
                table: "event_websites",
                type: "varchar(7)",
                maxLength: 7,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.Sql("UPDATE `website_sections` SET `Background` = 'secondary' WHERE `Background` = 'accent';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE `website_sections` SET `Background` = 'accent' WHERE `Background` = 'secondary';");

            migrationBuilder.DropColumn(
                name: "AccentColor",
                table: "event_websites");

            // The earlier model has no SecondaryColor property, so UpdateData can't be used here
            migrationBuilder.Sql("UPDATE `event_websites` SET `SecondaryColor` = '' WHERE `SecondaryColor` IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "SecondaryColor",
                table: "event_websites",
                type: "varchar(7)",
                maxLength: 7,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(7)",
                oldMaxLength: 7,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.RenameColumn(
                name: "SecondaryColor",
                table: "event_websites",
                newName: "AccentColor");
        }
    }
}
