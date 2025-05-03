using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventFlow.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryImage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImageUri",
                table: "Categories",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImageUri",
                table: "Categories");
        }
    }
}
