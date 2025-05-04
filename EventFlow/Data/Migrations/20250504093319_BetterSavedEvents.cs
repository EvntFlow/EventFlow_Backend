using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventFlow.Data.Migrations
{
    /// <inheritdoc />
    public partial class BetterSavedEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Interested",
                table: "Events",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Interested",
                table: "Events");
        }
    }
}
