using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventFlow.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketHolderInfo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HolderEmail",
                table: "Tickets",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "HolderFullName",
                table: "Tickets",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "HolderPhoneNumber",
                table: "Tickets",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HolderEmail",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "HolderFullName",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "HolderPhoneNumber",
                table: "Tickets");
        }
    }
}
