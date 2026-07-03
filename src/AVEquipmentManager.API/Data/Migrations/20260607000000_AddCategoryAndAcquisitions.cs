using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AVEquipmentManager.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryAndAcquisitions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Add Category column to Equipment
            migrationBuilder.AddColumn<int>(
                name: "Category",
                table: "Equipment",
                type: "INTEGER",
                nullable: false,
                defaultValue: 10);  // AssetCategory.Other = 10

            // 2. Create Acquisitions table
            migrationBuilder.CreateTable(
                name: "Acquisitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ItemName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Category = table.Column<int>(type: "INTEGER", nullable: false),
                    Vendor = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    PurchaseOrderNumber = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    UnitCost = table.Column<decimal>(type: "TEXT", nullable: true),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    IntendedRoom = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    RequestedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    OrderedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeployedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeployedEquipmentId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Acquisitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Acquisitions_Equipment_DeployedEquipmentId",
                        column: x => x.DeployedEquipmentId,
                        principalTable: "Equipment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Acquisitions_DeployedEquipmentId",
                table: "Acquisitions",
                column: "DeployedEquipmentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "Acquisitions");
            migrationBuilder.DropColumn(name: "Category", table: "Equipment");
        }
    }
}
