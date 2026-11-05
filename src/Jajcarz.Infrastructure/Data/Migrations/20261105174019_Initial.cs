using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jajcarz.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Devices",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Firmware = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Config = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Devices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Category = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    State = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Rev = table.Column<long>(type: "bigint", nullable: false),
                    ChangedBy = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ChangedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastChangeKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RevCounters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RevCounters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PanelKeys",
                columns: table => new
                {
                    DeviceId = table.Column<string>(type: "character varying(12)", nullable: false),
                    Addr = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    Present = table.Column<bool>(type: "boolean", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: true),
                    BoundAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PanelKeys", x => new { x.DeviceId, x.Addr });
                    table.ForeignKey(
                        name: "FK_PanelKeys_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PanelKeys_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.InsertData(
                table: "RevCounters",
                columns: new[] { "Id", "Value" },
                values: new object[] { 1, 0L });

            migrationBuilder.CreateIndex(
                name: "IX_Devices_TokenHash",
                table: "Devices",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PanelKeys_ProductId",
                table: "PanelKeys",
                column: "ProductId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PanelKeys");

            migrationBuilder.DropTable(
                name: "RevCounters");

            migrationBuilder.DropTable(
                name: "Devices");

            migrationBuilder.DropTable(
                name: "Products");
        }
    }
}
