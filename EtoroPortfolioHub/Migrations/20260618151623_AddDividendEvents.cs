using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EtoroPortfolioHub.Migrations
{
    /// <inheritdoc />
    public partial class AddDividendEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DividendEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    InstrumentId = table.Column<int>(type: "int", nullable: true),
                    Symbol = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CompanyName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Sector = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ExDividendDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PaymentDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AnnualDividend = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    PeriodicDividend = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DividendEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DividendEvents_UserId_Symbol_ExDividendDate_PaymentDate",
                table: "DividendEvents",
                columns: new[] { "UserId", "Symbol", "ExDividendDate", "PaymentDate" },
                unique: true,
                filter: "[ExDividendDate] IS NOT NULL AND [PaymentDate] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DividendEvents");
        }
    }
}
