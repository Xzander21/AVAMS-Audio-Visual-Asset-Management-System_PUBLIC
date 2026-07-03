using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AVEquipmentManager.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReservedStatusAndReplacement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReplacementEquipmentId",
                table: "Disposals",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Disposals_ReplacementEquipmentId",
                table: "Disposals",
                column: "ReplacementEquipmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Disposals_Equipment_ReplacementEquipmentId",
                table: "Disposals",
                column: "ReplacementEquipmentId",
                principalTable: "Equipment",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Disposals_Equipment_ReplacementEquipmentId",
                table: "Disposals");

            migrationBuilder.DropIndex(
                name: "IX_Disposals_ReplacementEquipmentId",
                table: "Disposals");

            migrationBuilder.DropColumn(
                name: "ReplacementEquipmentId",
                table: "Disposals");
        }
    }
}
