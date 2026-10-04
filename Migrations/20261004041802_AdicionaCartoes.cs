using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FinancasApi.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaCartoes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CreditCardId",
                table: "Expenses",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InstallmentGroupId",
                table: "Expenses",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<int>(
                name: "InstallmentNumber",
                table: "Expenses",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InstallmentTotal",
                table: "Expenses",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InvoiceMonth",
                table: "Expenses",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InvoiceYear",
                table: "Expenses",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Banks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Icon = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Color = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsSystem = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Banks", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "CreditCards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Nickname = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Brand = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Last4 = table.Column<string>(type: "varchar(4)", maxLength: 4, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Limit = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ClosingDay = table.Column<int>(type: "int", nullable: false),
                    DueDay = table.Column<int>(type: "int", nullable: false),
                    IsArchived = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    BankId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditCards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreditCards_Banks_BankId",
                        column: x => x.BankId,
                        principalTable: "Banks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CreditCards_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "InvoicePayments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    CreditCardId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PaidAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoicePayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvoicePayments_CreditCards_CreditCardId",
                        column: x => x.CreditCardId,
                        principalTable: "CreditCards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.InsertData(
                table: "Banks",
                columns: new[] { "Id", "Color", "Icon", "IsSystem", "Name", "UserId" },
                values: new object[,]
                {
                    { 1, "#8A05BE", "🏦", true, "Nubank", null },
                    { 2, "#EC0000", "🏦", true, "Santander", null },
                    { 3, "#CC092F", "🏦", true, "Bradesco", null },
                    { 4, "#EC7000", "🏦", true, "Itaú", null },
                    { 5, "#FCDD00", "🏦", true, "Banco do Brasil", null },
                    { 6, "#005CA9", "🏦", true, "Caixa", null },
                    { 7, "#0A2F5C", "🏦", true, "Safra", null },
                    { 8, "#FF7A00", "🏦", true, "Inter", null },
                    { 9, "#242424", "🏦", true, "C6 Bank", null },
                    { 10, "#001E62", "🏦", true, "BTG Pactual", null },
                    { 11, "#21C25E", "🏦", true, "PicPay", null },
                    { 12, "#009EE3", "🏦", true, "Mercado Pago", null },
                    { 13, "#00C2CB", "🏦", true, "Neon", null },
                    { 14, "#00D95F", "🏦", true, "Next", null },
                    { 15, "#3FA110", "🏦", true, "Sicredi", null },
                    { 16, "#003641", "🏦", true, "Sicoob", null },
                    { 17, "#004A9F", "🏦", true, "Banrisul", null },
                    { 18, "#1CA53B", "🏦", true, "Banco Original", null },
                    { 19, "#94A3B8", "🏦", true, "Outro", null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_CreditCardId_InvoiceYear_InvoiceMonth",
                table: "Expenses",
                columns: new[] { "CreditCardId", "InvoiceYear", "InvoiceMonth" });

            migrationBuilder.CreateIndex(
                name: "IX_CreditCards_BankId",
                table: "CreditCards",
                column: "BankId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditCards_UserId",
                table: "CreditCards",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoicePayments_CreditCardId_Year_Month",
                table: "InvoicePayments",
                columns: new[] { "CreditCardId", "Year", "Month" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Expenses_CreditCards_CreditCardId",
                table: "Expenses",
                column: "CreditCardId",
                principalTable: "CreditCards",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Expenses_CreditCards_CreditCardId",
                table: "Expenses");

            migrationBuilder.DropTable(
                name: "InvoicePayments");

            migrationBuilder.DropTable(
                name: "CreditCards");

            migrationBuilder.DropTable(
                name: "Banks");

            migrationBuilder.DropIndex(
                name: "IX_Expenses_CreditCardId_InvoiceYear_InvoiceMonth",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "CreditCardId",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "InstallmentGroupId",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "InstallmentNumber",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "InstallmentTotal",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "InvoiceMonth",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "InvoiceYear",
                table: "Expenses");
        }
    }
}
