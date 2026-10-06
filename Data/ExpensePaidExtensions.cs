using FinancasApi.Models;
using Microsoft.EntityFrameworkCore;

namespace FinancasApi.Data;

// Gastos de um mês:
// - Total: todos (pagos e pendentes)
// - PaidTotal: o que de fato já foi pago (inclui compras de cartão cuja fatura foi paga)
// - CountedTotal: gastos fora do cartão já pagos, que saem do saldo livre. Compras no cartão só saem do saldo
//   quando a fatura é paga (ver InvoicePaymentsInMonthAsync).
public record MonthExpenses(List<Expense> All, List<Expense> Counted, decimal Total, decimal CountedTotal, decimal PaidTotal)
{
    public decimal Pending => Total - PaidTotal;
}

public static class ExpensePaidExtensions
{
    // Despesas comuns e recorrentes ainda não pagas ficam como pendentes; compras no cartão dependem da fatura.
    public static async Task<MonthExpenses> MonthExpensesAsync(
        this AppDbContext db, int userId, int year, int month, bool includeCategory = false)
    {
        var q = db.Expenses.ForMonth(userId, year, month);
        if (includeCategory) q = q.Include(e => e.Category);
        var all = await q.ToListAsync();
        var paid = await db.PaidMapAsync(userId, all, year, month);

        var counted = all.Where(e => e.CreditCardId == null && paid.ContainsKey(e.Id)).ToList();
        var total = all.Sum(e => e.Amount);
        var countedTotal = counted.Sum(e => e.Amount);
        var paidTotal = all.Where(e => paid.ContainsKey(e.Id)).Sum(e => e.Amount);
        return new MonthExpenses(all, counted, total, countedTotal, paidTotal);
    }

    // Total de faturas de cartão pagas no mês (pela data do pagamento, no horário de Brasília): dinheiro que saiu do saldo
    public static async Task<decimal> InvoicePaymentsInMonthAsync(this AppDbContext db, int userId, int year, int month)
    {
        var start = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc).AddHours(3);
        var end = start.AddMonths(1);
        return await db.InvoicePayments
            .Where(p => p.UserId == userId && p.PaidAt >= start && p.PaidAt < end)
            .SumAsync(p => (decimal?)p.Amount) ?? 0;
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
