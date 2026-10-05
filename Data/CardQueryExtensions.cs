using FinancasApi.DTOs;
using FinancasApi.Models;
using FinancasApi.Services;
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

    // Fatura em destaque do cartão: a mais antiga fechada e ainda não paga (últimos meses), senão a do mês atual;
    // se a do mês atual já estiver paga, a do mês seguinte. Exige card.Bank carregado.
    public static async Task<InvoiceSummaryDto> CurrentInvoiceAsync(this AppDbContext db, int userId, CreditCard card)
    {
        var today = InvoiceCalculator.Today();
        var thisMonth = new DateOnly(today.Year, today.Month, 1);
        var payments = (await db.InvoicePayments.Where(p => p.CreditCardId == card.Id).ToListAsync())
            .ToDictionary(p => (p.Year, p.Month), p => p.PaidAt);

        async Task<InvoiceSummaryDto> Summarize(DateOnly inv)
        {
            var total = await db.InvoiceItems(userId, card.Id, inv.Year, inv.Month).SumAsync(e => (decimal?)e.Amount) ?? 0;
            var closing = InvoiceCalculator.ClosingDate(inv.Year, inv.Month, card.ClosingDay);
            var due = InvoiceCalculator.DueDate(inv.Year, inv.Month, card.ClosingDay, card.DueDay);
            DateTime? paidAt = payments.TryGetValue((inv.Year, inv.Month), out var at) ? at : null;
            var status = InvoiceCalculator.Status(today, closing, due, paidAt.HasValue);
            return new InvoiceSummaryDto(card.Id, card.Nickname, card.Bank.Name, card.Bank.Color,
                inv.Year, inv.Month, total, closing, due, status, paidAt);
        }

        // Faturas anteriores fechadas e sem pagamento (com valor) vêm primeiro: são as que o usuário precisa pagar
        foreach (var back in new[] { 2, 1 })
        {
            var s = await Summarize(thisMonth.AddMonths(-back));
            if (s.Status is "closed" or "overdue" && s.Total > 0) return s;
        }

        var current = await Summarize(thisMonth);
        return current.Status != "paid" ? current : await Summarize(thisMonth.AddMonths(1));
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
