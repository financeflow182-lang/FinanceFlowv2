using FinancasApi.Models;
using Microsoft.EntityFrameworkCore;

namespace FinancasApi.Data;

// Gastos de um mês separados em: efetivos (contam no saldo) e pendentes (ainda não pagos)
public record MonthExpenses(List<Expense> All, List<Expense> Counted, decimal Total, decimal CountedTotal, decimal Pending);

public static class ExpensePaidExtensions
{
    // Conta no gasto do mês: o que foi pago e as compras no cartão (já gastas na data da compra).
    // Despesas comuns e recorrentes ainda não pagas ficam como pendentes.
    public static async Task<MonthExpenses> MonthExpensesAsync(
        this AppDbContext db, int userId, int year, int month, bool includeCategory = false)
    {
        var q = db.Expenses.ForMonth(userId, year, month);
        if (includeCategory) q = q.Include(e => e.Category);
        var all = await q.ToListAsync();
        var paid = await db.PaidMapAsync(userId, all, year, month);

        var counted = all.Where(e => e.CreditCardId != null || paid.ContainsKey(e.Id)).ToList();
        var total = all.Sum(e => e.Amount);
        var countedTotal = counted.Sum(e => e.Amount);
        return new MonthExpenses(all, counted, total, countedTotal, total - countedTotal);
    }

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
