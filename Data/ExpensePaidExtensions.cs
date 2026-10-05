using FinancasApi.Models;
using Microsoft.EntityFrameworkCore;

namespace FinancasApi.Data;

public static class ExpensePaidExtensions
{
    // Despesas pagas entre as informadas (id -> data do pagamento), considerando o mês pedido para as recorrentes:
    // - comum: IsPaid
    // - recorrente: pagamento registrado para o mês (precisa de year/month)
    // - cartão: fatura paga (a do próprio item; para recorrente, a do mês pedido)
    public static async Task<Dictionary<int, DateTime?>> PaidMapAsync(
        this AppDbContext db, int userId, IReadOnlyCollection<Expense> items, int? year, int? month)
    {
        var result = new Dictionary<int, DateTime?>();

        foreach (var e in items.Where(e => e.CreditCardId == null && !e.IsRecurring && e.IsPaid))
            result[e.Id] = e.PaidAt;

        if (year.HasValue && month.HasValue)
        {
            var ids = items.Where(e => e.CreditCardId == null && e.IsRecurring).Select(e => e.Id).ToList();
            if (ids.Count > 0)
            {
                var y = year.Value; var m = month.Value;
                var pays = await db.ExpensePayments
                    .Where(p => p.UserId == userId && ids.Contains(p.ExpenseId) && p.Year == y && p.Month == m)
                    .ToListAsync();
                foreach (var p in pays) result[p.ExpenseId] = p.PaidAt;
            }
        }

        var cardIds = items.Where(e => e.CreditCardId != null).Select(e => e.CreditCardId!.Value).Distinct().ToList();
        if (cardIds.Count > 0)
        {
            var invoices = (await db.InvoicePayments.Where(p => cardIds.Contains(p.CreditCardId)).ToListAsync())
                .ToDictionary(p => (p.CreditCardId, p.Year, p.Month), p => p.PaidAt);

            foreach (var e in items.Where(e => e.CreditCardId != null))
            {
                (int, int, int)? key = e.IsRecurring
                    ? (year.HasValue && month.HasValue ? (e.CreditCardId!.Value, year.Value, month.Value) : null)
                    : (e.CreditCardId!.Value, e.InvoiceYear!.Value, e.InvoiceMonth!.Value);
                if (key.HasValue && invoices.TryGetValue(key.Value, out var paidAt))
                    result[e.Id] = paidAt;
            }
        }

        return result;
    }
}
