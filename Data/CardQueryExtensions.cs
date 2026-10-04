using FinancasApi.Models;
using Microsoft.EntityFrameworkCore;

namespace FinancasApi.Data;

public static class CardQueryExtensions
{
    // Itens da fatura (identificada pelo mês de fechamento): compras daquela fatura + recorrentes iniciadas até ela
    public static IQueryable<Expense> InvoiceItems(this AppDbContext db, int userId, int cardId, int year, int month)
    {
        var idx = year * 12 + month;
        return db.Expenses.Where(e => e.UserId == userId && e.CreditCardId == cardId &&
            ((e.InvoiceYear == year && e.InvoiceMonth == month) ||
             (e.IsRecurring && e.InvoiceYear!.Value * 12 + e.InvoiceMonth!.Value <= idx)));
    }

    // Limite usado: compras em faturas ainda não pagas (inclui parcelas futuras); recorrentes contam um mês
    public static async Task<decimal> UsedLimitAsync(this AppDbContext db, int userId, int cardId)
    {
        var purchases = await db.Expenses
            .Where(e => e.UserId == userId && e.CreditCardId == cardId)
            .Select(e => new { e.Amount, e.IsRecurring, e.InvoiceYear, e.InvoiceMonth })
            .ToListAsync();
        var paid = (await db.InvoicePayments
            .Where(p => p.CreditCardId == cardId)
            .Select(p => new { p.Year, p.Month })
            .ToListAsync())
            .Select(p => (p.Year, p.Month))
            .ToHashSet();

        return purchases.Where(p => !p.IsRecurring && !paid.Contains((p.InvoiceYear!.Value, p.InvoiceMonth!.Value))).Sum(p => p.Amount)
            + purchases.Where(p => p.IsRecurring).Sum(p => p.Amount);
    }
}
