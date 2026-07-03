using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AVEquipmentManager.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddExternalTicketId : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalTicketId",
                table: "Tickets",
                type: "TEXT",
                maxLength: 100,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ExternalTicketId", table: "Tickets");
        }
    }
}
