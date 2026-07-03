using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AVEquipmentManager.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEquipmentAccessoryFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasAppleTv",
                table: "Equipment",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasRemoteHolder",
                table: "Equipment",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasWallSpeaker",
                table: "Equipment",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HasAppleTv",
                table: "Equipment");

            migrationBuilder.DropColumn(
                name: "HasRemoteHolder",
                table: "Equipment");

            migrationBuilder.DropColumn(
                name: "HasWallSpeaker",
                table: "Equipment");
        }
    }
}
