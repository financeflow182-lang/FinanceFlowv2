using FinancasApi.Data;
using FinancasApi.Models;
using Microsoft.EntityFrameworkCore;

namespace FinancasApi.Services;

public class AlertService(AppDbContext db, IConfiguration config)
{
    private readonly decimal _threshold = decimal.Parse(config["AlertSettings:SpendingThresholdPercent"]!);

    public async Task CheckAndCreateAlertsAsync(int userId, int year, int month)
    {
        var budget = await db.MonthlyBudgets
            .FirstOrDefaultAsync(b => b.UserId == userId && b.Year == year && b.Month == month);
        var otherIncome = await db.Incomes
            .Where(i => i.UserId == userId && i.Date.Year == year && i.Date.Month == month)
            .SumAsync(i => (decimal?)i.Amount) ?? 0;
        var income = (budget?.Salary ?? 0) + otherIncome;
        if (income == 0) return;

        var totalExp = await db.Expenses
            .ForMonth(userId, year, month)
            .SumAsync(e => e.Amount);

        var pct = totalExp / income * 100;

        // Alerta de gastos elevados ou orçamento estourado
        if (pct >= _threshold)
        {
            var level = pct >= 100 ? "danger" : "warning";
            var title = pct >= 100 ? "⚠️ Orçamento estourado!" : "🔔 Gastos elevados";
            var msg   = pct >= 100
                ? $"Seus gastos ({pct:F0}%) já ultrapassaram a receita em {month:00}/{year}."
                : $"Você já usou {pct:F0}% da receita em {month:00}/{year}. Fique de olho!";

            var already = await db.Alerts.AnyAsync(a =>
                a.UserId == userId && a.Type == level &&
                a.CreatedAt.Year == year && a.CreatedAt.Month == month &&
                a.Title == title);

            if (!already)
                db.Alerts.Add(new Alert { UserId = userId, Title = title, Message = msg, Type = level });
        }

        // Alerta de gastos elevados por categoria — se qualquer categoria exceder 40% da receita
        var catTotals = await db.Expenses
            .ForMonth(userId, year, month)
            .GroupBy(e => new { e.CategoryId, e.Category.Name, e.Category.Icon })
            .Select(g => new { g.Key.Name, g.Key.Icon, Total = g.Sum(e => e.Amount) })
            .ToListAsync();

        foreach (var cat in catTotals.Where(c => c.Total / income * 100 >= 40))
        {
            var title = $"{cat.Icon} Gasto alto em {cat.Name}";
            var already = await db.Alerts.AnyAsync(a =>
                a.UserId == userId && a.Title == title &&
                a.CreatedAt.Year == year && a.CreatedAt.Month == month);
            if (!already)
                db.Alerts.Add(new Alert
                {
                    UserId = userId, Title = title, Type = "warning",
                    Message = $"A categoria {cat.Name} consumiu {cat.Total / income * 100:F0}% da sua receita este mês."
                });
        }

        await db.SaveChangesAsync();
    }

    // Alertas de cartão: limite quase esgotado/esgotado e fatura vencendo/vencida
    public async Task CheckCardAlertsAsync(int userId)
    {
        var cards = await db.CreditCards.Where(c => c.UserId == userId && !c.IsArchived).ToListAsync();
        if (cards.Count == 0) return;

        var today = InvoiceCalculator.Today();
        var now = DateTime.UtcNow;

        foreach (var card in cards)
        {
            // Limite: um alerta por nível e por mês
            var used = await db.UsedLimitAsync(userId, card.Id);
            var pct = card.Limit > 0 ? used / card.Limit * 100 : 0;
            if (pct >= _threshold)
            {
                var exhausted = pct >= 100;
                var title = exhausted ? $"🚨 Limite esgotado: {card.Nickname}" : $"💳 Limite alto: {card.Nickname}";
                var already = await db.Alerts.AnyAsync(a =>
                    a.UserId == userId && a.Title == title &&
                    a.CreatedAt.Year == now.Year && a.CreatedAt.Month == now.Month);
                if (!already)
                    db.Alerts.Add(new Alert
                    {
                        UserId = userId, Title = title, Type = exhausted ? "danger" : "warning",
                        Message = exhausted
                            ? $"O limite do cartão {card.Nickname} acabou ({pct:F0}% usado)."
                            : $"Você já usou {pct:F0}% do limite do cartão {card.Nickname}."
                    });
            }

            // Faturas fechadas e não pagas dos últimos meses: vence em até 3 dias ou já venceu
            var (curYear, curMonth) = InvoiceCalculator.InvoiceFor(today, card.ClosingDay);
            var current = new DateOnly(curYear, curMonth, 1);
            for (var back = 0; back <= 2; back++)
            {
                var inv = current.AddMonths(-back);
                var due = InvoiceCalculator.DueDate(inv.Year, inv.Month, card.ClosingDay, card.DueDay);
                var daysLeft = due.DayNumber - today.DayNumber;
                if (daysLeft > 3) continue;

                if (await db.InvoicePayments.AnyAsync(p => p.CreditCardId == card.Id && p.Year == inv.Year && p.Month == inv.Month))
                    continue;

                var total = await db.InvoiceItems(userId, card.Id, inv.Year, inv.Month).SumAsync(e => (decimal?)e.Amount) ?? 0;
                if (total <= 0) continue;

                var overdue = daysLeft < 0;
                var title = overdue
                    ? $"⚠️ Fatura vencida: {card.Nickname} ({due:dd/MM})"
                    : $"📅 Fatura vencendo: {card.Nickname} ({due:dd/MM})";
                var exists = await db.Alerts.AnyAsync(a => a.UserId == userId && a.Title == title);
                if (exists) continue;

                db.Alerts.Add(new Alert
                {
                    UserId = userId, Title = title, Type = overdue ? "danger" : "warning",
                    Message = overdue
                        ? $"A fatura de R$ {total:N2} do cartão {card.Nickname} venceu em {due:dd/MM} e não está paga."
                        : daysLeft == 0
                            ? $"A fatura de R$ {total:N2} do cartão {card.Nickname} vence hoje."
                            : $"A fatura de R$ {total:N2} do cartão {card.Nickname} vence em {daysLeft} dia(s), em {due:dd/MM}."
                });
            }
        }

        await db.SaveChangesAsync();
    }

    public async Task CheckGoalAlerts(int userId, int goalId)
    {
        var goal = await db.Goals.FindAsync(goalId); //
        if (goal == null || goal.UserId != userId || goal.TargetAmount <= 0) return;

        var pct = goal.CurrentAmount / goal.TargetAmount * 100;

        if (pct >= 100 && !goal.IsCompleted)
        {
            goal.IsCompleted = true;
            db.Alerts.Add(new Alert
            {
                UserId = userId, Type = "info",
                Title = $"🎯 Meta concluída: {goal.Name}!",
                Message = $"Parabéns! Você atingiu sua meta de R$ {goal.TargetAmount:N2}."
            });
        }
        else if (pct >= 75)
        {
            var title = $"🏁 Quase lá: {goal.Name}";
            var already = await db.Alerts.AnyAsync(a => a.UserId == userId && a.Title == title);
            if (!already)
                db.Alerts.Add(new Alert
                {
                    UserId = userId, Type = "info", Title = title,
                    Message = $"Você já atingiu {pct:F0}% da sua meta '{goal.Name}'!"
                });
        }

        await db.SaveChangesAsync();
    }
}
