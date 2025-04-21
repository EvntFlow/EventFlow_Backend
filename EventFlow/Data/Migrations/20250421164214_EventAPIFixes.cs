using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventFlow.Data.Migrations
{
    /// <inheritdoc />
    public partial class EventAPIFixes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "Event",
                newName: "Events");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "TicketOptions",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "BannerUri",
                table: "Events",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.CreateIndex(
                name: "IX_SavedEvents_AttendeeId_EventId",
                table: "SavedEvents",
                columns: new[] { "AttendeeId", "EventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EventCategories_EventId_CategoryId",
                table: "EventCategories",
                columns: new[] { "EventId", "CategoryId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SavedEvents_AttendeeId_EventId",
                table: "SavedEvents");

            migrationBuilder.DropIndex(
                name: "IX_EventCategories_EventId_CategoryId",
                table: "EventCategories");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "TicketOptions",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "BannerUri",
                table: "Events",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.RenameTable(
                name: "Events",
                newName: "Event");
        }
    }
}
