using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EtoroPortfolioHub.Migrations
{
    /// <inheritdoc />
    public partial class ChangeTargetPercentageToInt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "TargetPercentage",
                table: "PortfolioTargets",
                type: "int",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,4)",
                oldPrecision: 18,
                oldScale: 4);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "TargetPercentage",
                table: "PortfolioTargets",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");
        }
    }
}
