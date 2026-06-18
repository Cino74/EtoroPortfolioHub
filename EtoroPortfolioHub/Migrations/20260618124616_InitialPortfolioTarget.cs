using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EtoroPortfolioHub.Migrations
{
    /// <inheritdoc />
    public partial class InitialPortfolioTarget : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PortfolioTargets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    InstrumentId = table.Column<int>(type: "int", nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    InstrumentName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    TargetPercentage = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortfolioTargets", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioTargets_UserId_InstrumentId",
                table: "PortfolioTargets",
                columns: new[] { "UserId", "InstrumentId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PortfolioTargets");
        }
    }
}
