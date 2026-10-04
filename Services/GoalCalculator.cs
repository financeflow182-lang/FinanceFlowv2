using FinancasApi.DTOs;
using FinancasApi.Models;

namespace FinancasApi.Services;

// Cálculos de planejamento de metas (soma simples, sem juros)
public static class GoalCalculator
{
    private static readonly int[] ScenarioMonths = [3, 6, 12, 24];

    public static GoalDto ToDto(Goal g)
    {
        var today = InvoiceCalculator.Today();
        var remaining = Math.Max(g.TargetAmount - g.CurrentAmount, 0);
        var done = g.IsCompleted || remaining == 0;

        int? monthsLeft = null;
        decimal? required = null;
        string status;

        if (done)
        {
            status = "completed";
            if (g.Deadline.HasValue) required = 0;
        }
        else if (g.Deadline is { } deadline)
        {
            if (deadline < today)
            {
                status = "overdue";
                monthsLeft = 0;
            }
            else
            {
                monthsLeft = Math.Max((deadline.Year - today.Year) * 12 + deadline.Month - today.Month, 1);
                required = CeilCents(remaining / monthsLeft.Value);

                // Ritmo: quanto deveria ter guardado até hoje, em linha reta entre a criação e o prazo
                var created = DateOnly.FromDateTime(g.CreatedAt);
                var total = deadline.DayNumber - created.DayNumber;
                var elapsed = today.DayNumber - created.DayNumber;
                var fraction = total <= 0 ? 1m : Math.Clamp((decimal)elapsed / total, 0m, 1m);
                status = g.CurrentAmount >= g.TargetAmount * fraction ? "on_track" : "behind";
            }
        }
        else
        {
            status = "no_deadline";
        }

        DateOnly? projected = null;
        bool? plannedMeets = null;
        if (!done && g.PlannedMonthly is > 0)
        {
            var months = (int)Math.Min(Math.Ceiling(remaining / g.PlannedMonthly.Value), 1200);
            projected = MonthStart(today).AddMonths(months);
            if (required.HasValue) plannedMeets = g.PlannedMonthly.Value >= required.Value;
        }

        var scenarios = new List<GoalScenarioDto>();
        if (!done)
        {
            foreach (var m in ScenarioMonths)
            {
                var monthly = CeilCents(remaining / m);
                if (monthly < 1) continue;
                scenarios.Add(new GoalScenarioDto(m, monthly, MonthStart(today).AddMonths(m)));
            }
        }

        return new GoalDto(
            g.Id, g.Name, g.Icon, g.TargetAmount, g.CurrentAmount, g.Deadline, g.IsCompleted,
            g.TargetAmount > 0 ? Math.Round(g.CurrentAmount / g.TargetAmount * 100, 1) : 0,
            g.CreatedAt,
            g.PlannedMonthly, remaining, monthsLeft, required, status, projected, plannedMeets, scenarios);
    }

    private static decimal CeilCents(decimal v) => Math.Ceiling(v * 100) / 100;
    private static DateOnly MonthStart(DateOnly d) => new(d.Year, d.Month, 1);
}
