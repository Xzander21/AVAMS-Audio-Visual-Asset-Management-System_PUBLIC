using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AVEquipmentManager.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddArchiveFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Equipment archive columns
            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "Equipment",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAt",
                table: "Equipment",
                type: "TEXT",
                nullable: true);

            // Staff archive columns
            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "Staff",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAt",
                table: "Staff",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ArchivedAt", table: "Equipment");
            migrationBuilder.DropColumn(name: "IsArchived", table: "Equipment");
            migrationBuilder.DropColumn(name: "ArchivedAt", table: "Staff");
            migrationBuilder.DropColumn(name: "IsArchived", table: "Staff");
        }
    }
}
