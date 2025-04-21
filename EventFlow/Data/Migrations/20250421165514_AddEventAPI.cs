using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventFlow.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEventAPI : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Event_Organizers_OrganizerId",
                table: "Event");

            migrationBuilder.DropForeignKey(
                name: "FK_EventCategory_Categories_CategoryId",
                table: "EventCategory");

            migrationBuilder.DropForeignKey(
                name: "FK_EventCategory_Event_EventId",
                table: "EventCategory");

            migrationBuilder.DropForeignKey(
                name: "FK_SavedEvents_Event_EventId",
                table: "SavedEvents");

            migrationBuilder.DropForeignKey(
                name: "FK_TicketOptions_Event_EventId",
                table: "TicketOptions");

            migrationBuilder.DropIndex(
                name: "IX_SavedEvents_AttendeeId",
                table: "SavedEvents");

            migrationBuilder.DropPrimaryKey(
                name: "PK_EventCategory",
                table: "EventCategory");

            migrationBuilder.DropIndex(
                name: "IX_EventCategory_EventId",
                table: "EventCategory");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Event",
                table: "Event");

            migrationBuilder.RenameTable(
                name: "EventCategory",
                newName: "EventCategories");

            migrationBuilder.RenameTable(
                name: "Event",
                newName: "Events");

            migrationBuilder.RenameIndex(
                name: "IX_EventCategory_CategoryId",
                table: "EventCategories",
                newName: "IX_EventCategories_CategoryId");

            migrationBuilder.RenameColumn(
                name: "Date",
                table: "Events",
                newName: "StartDate");

            migrationBuilder.RenameIndex(
                name: "IX_Event_OrganizerId",
                table: "Events",
                newName: "IX_Events_OrganizerId");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "TicketOptions",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "BannerUri",
                table: "Events",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EndDate",
                table: "Events",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddPrimaryKey(
                name: "PK_EventCategories",
                table: "EventCategories",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Events",
                table: "Events",
                column: "Id");

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

            migrationBuilder.AddForeignKey(
                name: "FK_EventCategories_Categories_CategoryId",
                table: "EventCategories",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_EventCategories_Events_EventId",
                table: "EventCategories",
                column: "EventId",
                principalTable: "Events",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Events_Organizers_OrganizerId",
                table: "Events",
                column: "OrganizerId",
                principalTable: "Organizers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SavedEvents_Events_EventId",
                table: "SavedEvents",
                column: "EventId",
                principalTable: "Events",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TicketOptions_Events_EventId",
                table: "TicketOptions",
                column: "EventId",
                principalTable: "Events",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EventCategories_Categories_CategoryId",
                table: "EventCategories");

            migrationBuilder.DropForeignKey(
                name: "FK_EventCategories_Events_EventId",
                table: "EventCategories");

            migrationBuilder.DropForeignKey(
                name: "FK_Events_Organizers_OrganizerId",
                table: "Events");

            migrationBuilder.DropForeignKey(
                name: "FK_SavedEvents_Events_EventId",
                table: "SavedEvents");

            migrationBuilder.DropForeignKey(
                name: "FK_TicketOptions_Events_EventId",
                table: "TicketOptions");

            migrationBuilder.DropIndex(
                name: "IX_SavedEvents_AttendeeId_EventId",
                table: "SavedEvents");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Events",
                table: "Events");

            migrationBuilder.DropPrimaryKey(
                name: "PK_EventCategories",
                table: "EventCategories");

            migrationBuilder.DropIndex(
                name: "IX_EventCategories_EventId_CategoryId",
                table: "EventCategories");

            migrationBuilder.DropColumn(
                name: "BannerUri",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "EndDate",
                table: "Events");

            migrationBuilder.RenameTable(
                name: "Events",
                newName: "Event");

            migrationBuilder.RenameTable(
                name: "EventCategories",
                newName: "EventCategory");

            migrationBuilder.RenameColumn(
                name: "StartDate",
                table: "Event",
                newName: "Date");

            migrationBuilder.RenameIndex(
                name: "IX_Events_OrganizerId",
                table: "Event",
                newName: "IX_Event_OrganizerId");

            migrationBuilder.RenameIndex(
                name: "IX_EventCategories_CategoryId",
                table: "EventCategory",
                newName: "IX_EventCategory_CategoryId");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "TicketOptions",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Event",
                table: "Event",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_EventCategory",
                table: "EventCategory",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_SavedEvents_AttendeeId",
                table: "SavedEvents",
                column: "AttendeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EventCategory_EventId",
                table: "EventCategory",
                column: "EventId");

            migrationBuilder.AddForeignKey(
                name: "FK_Event_Organizers_OrganizerId",
                table: "Event",
                column: "OrganizerId",
                principalTable: "Organizers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_EventCategory_Categories_CategoryId",
                table: "EventCategory",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_EventCategory_Event_EventId",
                table: "EventCategory",
                column: "EventId",
                principalTable: "Event",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SavedEvents_Event_EventId",
                table: "SavedEvents",
                column: "EventId",
                principalTable: "Event",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TicketOptions_Event_EventId",
                table: "TicketOptions",
                column: "EventId",
                principalTable: "Event",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
