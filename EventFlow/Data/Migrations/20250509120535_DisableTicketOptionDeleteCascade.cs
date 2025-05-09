using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventFlow.Data.Migrations
{
    /// <inheritdoc />
    public partial class DisableTicketOptionDeleteCascade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_Attendees_AttendeeId",
                table: "Tickets");

            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_TicketOptions_TicketOptionId",
                table: "Tickets");

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_Attendees_AttendeeId",
                table: "Tickets",
                column: "AttendeeId",
                principalTable: "Attendees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_TicketOptions_TicketOptionId",
                table: "Tickets",
                column: "TicketOptionId",
                principalTable: "TicketOptions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_Attendees_AttendeeId",
                table: "Tickets");

            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_TicketOptions_TicketOptionId",
                table: "Tickets");

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_Attendees_AttendeeId",
                table: "Tickets",
                column: "AttendeeId",
                principalTable: "Attendees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_TicketOptions_TicketOptionId",
                table: "Tickets",
                column: "TicketOptionId",
                principalTable: "TicketOptions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
