using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AVEquipmentManager.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class TxProofing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Disposals_EquipmentId",
                table: "Disposals");

            migrationBuilder.AlterColumn<string>(
                name: "Role",
                table: "Users",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 20,
                oldDefaultValue: "Student");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Tickets",
                type: "BLOB",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Equipment",
                type: "BLOB",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Disposals",
                type: "BLOB",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Acquisitions",
                type: "BLOB",
                rowVersion: true,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LifecycleLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EntityType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    EntityId = table.Column<int>(type: "INTEGER", nullable: false),
                    FromStatus = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ToStatus = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    PerformedByUserId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    TransitionedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LifecycleLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Disposals_OpenPerEquipment",
                table: "Disposals",
                columns: new[] { "EquipmentId", "Status" },
                unique: true,
                filter: "\"Status\" IN (0, 1)");

            migrationBuilder.CreateIndex(
                name: "IX_LifecycleLogs_EntityHistory",
                table: "LifecycleLogs",
                columns: new[] { "EntityType", "EntityId", "TransitionedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LifecycleLogs");

            migrationBuilder.DropIndex(
                name: "IX_Disposals_OpenPerEquipment",
                table: "Disposals");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Equipment");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Disposals");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Acquisitions");

            migrationBuilder.AlterColumn<string>(
                name: "Role",
                table: "Users",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "Student",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 20);

            migrationBuilder.CreateIndex(
                name: "IX_Disposals_EquipmentId",
                table: "Disposals",
                column: "EquipmentId");
        }
    }
}
