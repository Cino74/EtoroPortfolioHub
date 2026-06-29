using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EtoroPortfolioHub.Migrations
{
    /// <inheritdoc />
    public partial class AddEtoroConnection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EtoroConnections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Environment = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PermissionMode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    EncryptedUserKey = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Gcid = table.Column<long>(type: "bigint", nullable: true),
                    RealCid = table.Column<long>(type: "bigint", nullable: true),
                    DemoCid = table.Column<long>(type: "bigint", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSuccessfulValidationUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastValidationMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EtoroConnections", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EtoroConnections_UserId",
                table: "EtoroConnections",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EtoroConnections");
        }
    }
}
