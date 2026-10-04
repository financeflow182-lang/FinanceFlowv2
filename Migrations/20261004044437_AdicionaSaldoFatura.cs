using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinancasApi.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaSaldoFatura : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ExcludeFromBudget",
                table: "Expenses",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsInvoiceBalance",
                table: "Expenses",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExcludeFromBudget",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "IsInvoiceBalance",
                table: "Expenses");
        }
    }
}
