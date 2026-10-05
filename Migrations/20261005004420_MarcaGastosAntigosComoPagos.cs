using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinancasApi.Migrations
{
    /// <inheritdoc />
    public partial class MarcaGastosAntigosComoPagos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Antes do controle de pagamento todo gasto já contava no mês. Para não zerar o dashboard,
            // gastos comuns (fora de cartão) com data até hoje passam a constar como pagos.
            migrationBuilder.Sql(
                "UPDATE `Expenses` SET `IsPaid` = 1, `PaidAt` = `CreatedAt` " +
                "WHERE `CreditCardId` IS NULL AND `IsRecurring` = 0 AND `IsPaid` = 0 AND `Date` <= CURDATE();");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Não há como distinguir quais foram marcados por esta migration; nada a desfazer.
        }
    }
}
