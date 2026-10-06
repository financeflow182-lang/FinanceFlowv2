using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinancasApi.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaPatrimonio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsExistingBalance",
                table: "Investments",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsExistingBalance",
                table: "Investments");
        }
    }
}
