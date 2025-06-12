using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLedger.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInterestRateToAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "InterestRate",
                table: "Accounts",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InterestRate",
                table: "Accounts");
        }
    }
}
