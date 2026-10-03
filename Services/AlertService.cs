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
        if (budget == null || budget.Salary == 0) return;

        var totalExp = await db.Expenses
            .Where(e => e.UserId == userId && e.Date.Year == year && e.Date.Month == month)
            .SumAsync(e => e.Amount);

        var pct = totalExp / budget.Salary * 100;

        // Alerta de gastos elevados ou orçamento estourado
        if (pct >= _threshold)
        {
            var level = pct >= 100 ? "danger" : "warning";
            var title = pct >= 100 ? "⚠️ Orçamento estourado!" : "🔔 Gastos elevados";
            var msg   = pct >= 100
                ? $"Seus gastos ({pct:F0}%) já ultrapassaram o salário em {month:00}/{year}."
                : $"Você já usou {pct:F0}% do salário em {month:00}/{year}. Fique de olho!";

            var already = await db.Alerts.AnyAsync(a =>
                a.UserId == userId && a.Type == level &&
                a.CreatedAt.Year == year && a.CreatedAt.Month == month &&
                a.Title == title);

            if (!already)
                db.Alerts.Add(new Alert { UserId = userId, Title = title, Message = msg, Type = level });
        }

        // Alerta de gastos elevados por categoria — se qualquer categoria exceder 40% do salário
        var catTotals = await db.Expenses
            .Where(e => e.UserId == userId && e.Date.Year == year && e.Date.Month == month)
            .GroupBy(e => new { e.CategoryId, e.Category.Name, e.Category.Icon })
            .Select(g => new { g.Key.Name, g.Key.Icon, Total = g.Sum(e => e.Amount) })
            .ToListAsync();

        foreach (var cat in catTotals.Where(c => c.Total / budget.Salary * 100 >= 40))
        {
            var title = $"{cat.Icon} Gasto alto em {cat.Name}";
            var already = await db.Alerts.AnyAsync(a =>
                a.UserId == userId && a.Title == title &&
                a.CreatedAt.Year == year && a.CreatedAt.Month == month);
            if (!already)
                db.Alerts.Add(new Alert
                {
                    UserId = userId, Title = title, Type = "warning",
                    Message = $"A categoria {cat.Name} consumiu {cat.Total / budget.Salary * 100:F0}% do seu salário este mês."
                });
        }

        await db.SaveChangesAsync();
    }

    public async Task CheckGoalAlerts(int userId, int goalId)
    {
        var goal = await db.Goals.FindAsync(goalId); //
        if (goal == null) return;

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
