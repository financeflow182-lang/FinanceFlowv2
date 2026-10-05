using FinancasApi.Data;
using FinancasApi.DTOs;
using FinancasApi.Models;
using FinancasApi.Services;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinancasApi.Controllers;

// Criaçãp das categorias, investimentos, metas, alertas e dashboard
[Route("api/categories")]
public class CategoriesController(AppDbContext db) : BaseController
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<CategoryDto>>> List()
    {
        var cats = await db.Categories
            .Where(c => c.IsSystem || c.UserId == UserId)
            .OrderBy(c => c.Name)
            .ToListAsync();
        return Ok(cats.Select(c => new CategoryDto(c.Id, c.Name, c.Icon, c.Color, c.IsSystem)));
    }

    [HttpPost]
    public async Task<ActionResult<CategoryDto>> Create(CreateCategoryRequest req)
    {
        if (await db.Categories.CountAsync(c => c.UserId == UserId) >= 100)
            return BadRequest(new { message = "Limite de categorias atingido." });

        var cat = new Category { Name = req.Name, Icon = req.Icon, Color = req.Color, UserId = UserId };
        db.Categories.Add(cat);
        await db.SaveChangesAsync();
        return Ok(new CategoryDto(cat.Id, cat.Name, cat.Icon, cat.Color, cat.IsSystem));
    }
}


[Route("api/investments")]
public class InvestmentsController(AppDbContext db) : BaseController
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<InvestmentDto>>> List(
        [FromQuery, Range(2000, 2100)] int? year, [FromQuery, Range(1, 12)] int? month)
    {
        var q = db.Investments.Where(i => i.UserId == UserId);
        if (year.HasValue) q = q.Where(i => i.Date.Year == year.Value);
        if (month.HasValue) q = q.Where(i => i.Date.Month == month.Value);
        var list = await q.OrderByDescending(i => i.Date).Take(1000).ToListAsync();
        return Ok(list.Select(ToDto));
    }

    [HttpPost]
    public async Task<ActionResult<InvestmentDto>> Create(CreateInvestmentRequest req)
    {
        var inv = new Investment { Name = req.Name, Type = req.Type, Amount = req.Amount, Date = req.Date, UserId = UserId };
        db.Investments.Add(inv);
        await db.SaveChangesAsync();
        return Ok(ToDto(inv));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<InvestmentDto>> Update(int id, UpdateInvestmentRequest req)
    {
        var inv = await db.Investments.FirstOrDefaultAsync(i => i.Id == id && i.UserId == UserId);
        if (inv == null) return NotFound();
        inv.Name = req.Name; inv.Type = req.Type; inv.Amount = req.Amount; inv.Date = req.Date;
        await db.SaveChangesAsync();
        return Ok(ToDto(inv));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var inv = await db.Investments.FirstOrDefaultAsync(i => i.Id == id && i.UserId == UserId);
        if (inv == null) return NotFound();
        db.Investments.Remove(inv);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static InvestmentDto ToDto(Investment i) =>
        new(i.Id, i.Name, i.Type, i.Amount, i.Date, i.CreatedAt);
}


[Route("api/goals")]
public class GoalsController(AppDbContext db, AlertService alerts) : BaseController
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<GoalDto>>> List()
    {
        var list = await db.Goals.Where(g => g.UserId == UserId).OrderByDescending(g => g.CreatedAt).Take(200).ToListAsync();
        return Ok(list.Select(ToDto));
    }

    [HttpPost]
    public async Task<ActionResult<GoalDto>> Create(CreateGoalRequest req)
    {
        if (await db.Goals.CountAsync(g => g.UserId == UserId) >= 200)
            return BadRequest(new { message = "Limite de metas atingido." });

        if (req.Deadline.HasValue && req.Deadline.Value < InvoiceCalculator.Today())
            return BadRequest(new { message = "O prazo da meta não pode estar no passado." });

        var goal = new Goal
        {
            Name = req.Name.Trim(), Icon = req.Icon, TargetAmount = req.TargetAmount,
            Deadline = req.Deadline, PlannedMonthly = req.PlannedMonthly, UserId = UserId
        };
        db.Goals.Add(goal);
        await db.SaveChangesAsync();
        return Ok(ToDto(goal));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<GoalDto>> Update(int id, UpdateGoalRequest req)
    {
        var goal = await db.Goals.FirstOrDefaultAsync(g => g.Id == id && g.UserId == UserId);
        if (goal == null) return NotFound();
        if (req.Deadline.HasValue && req.Deadline.Value < InvoiceCalculator.Today())
            return BadRequest(new { message = "O prazo da meta não pode estar no passado." });

        goal.Name = req.Name.Trim(); goal.Icon = req.Icon; goal.TargetAmount = req.TargetAmount;
        goal.Deadline = req.Deadline; goal.PlannedMonthly = req.PlannedMonthly;
        // Reabre a meta se o valor alvo ou o progresso mudou de modo que ela não esteja mais concluída
        if (goal.IsCompleted && goal.CurrentAmount < goal.TargetAmount) goal.IsCompleted = false;
        await db.SaveChangesAsync();
        return Ok(ToDto(goal));
    }

    [HttpPost("{id}/deposit")]
    public async Task<ActionResult<GoalDto>> Deposit(int id, AddToGoalRequest req)
    {
        var goal = await db.Goals.FirstOrDefaultAsync(g => g.Id == id && g.UserId == UserId);
        if (goal == null) return NotFound();
        if (goal.CurrentAmount + req.Amount > (decimal)Limits.MaxMoney)
            return BadRequest(new { message = "Valor acima do limite permitido." });
        goal.CurrentAmount += req.Amount;
        db.GoalDeposits.Add(new GoalDeposit { GoalId = goal.Id, UserId = UserId, Amount = req.Amount, Date = InvoiceCalculator.Today() });
        await db.SaveChangesAsync();
        await alerts.CheckGoalAlerts(UserId, id);
        return Ok(ToDto(goal));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var goal = await db.Goals.FirstOrDefaultAsync(g => g.Id == id && g.UserId == UserId);
        if (goal == null) return NotFound();
        db.Goals.Remove(goal);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static GoalDto ToDto(Goal g) => GoalCalculator.ToDto(g);
}


[Route("api/alerts")]
public class AlertsController(AppDbContext db) : BaseController
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<AlertDto>>> List([FromQuery] bool unreadOnly = false)
    {
        var q = db.Alerts.Where(a => a.UserId == UserId);
        if (unreadOnly) q = q.Where(a => !a.IsRead);
        var list = await q.OrderByDescending(a => a.CreatedAt).Take(50).ToListAsync();
        return Ok(list.Select(ToDto));
    }

    [HttpPatch("{id}/read")]
    public async Task<IActionResult> MarkRead(int id)
    {
        var alert = await db.Alerts.FirstOrDefaultAsync(a => a.Id == id && a.UserId == UserId);
        if (alert == null) return NotFound();
        alert.IsRead = true;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPatch("read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        await db.Alerts.Where(a => a.UserId == UserId && !a.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.IsRead, true));
        return NoContent();
    }

    private static AlertDto ToDto(Alert a) =>
        new(a.Id, a.Title, a.Message, a.Type, a.IsRead, a.CreatedAt);
}


[Route("api/dashboard")]
public class DashboardController(AppDbContext db, AlertService alerts) : BaseController
{
    [HttpGet("{year}/{month}")]
    public async Task<ActionResult<DashboardDto>> Get(
        [Range(2000, 2100)] int year, [Range(1, 12)] int month)
    {
        // Alertas de cartão dependem da data (vencimentos), então são verificados ao abrir o dashboard
        await alerts.CheckCardAlertsAsync(UserId);

        // Budget
        var budget = await db.MonthlyBudgets
            .FirstOrDefaultAsync(b => b.UserId == UserId && b.Year == year && b.Month == month);
        var salary = budget?.Salary ?? 0;

        var monthExp = await db.MonthExpensesAsync(UserId, year, month, includeCategory: true);
        var expenses = monthExp.Counted;
        var investments = await db.Investments
            .Where(i => i.UserId == UserId && i.Date.Year == year && i.Date.Month == month)
            .ToListAsync();

        var otherIncome = await db.Incomes
            .Where(i => i.UserId == UserId && i.Date.Year == year && i.Date.Month == month)
            .SumAsync(i => (decimal?)i.Amount) ?? 0;

        var totalIncome = salary + otherIncome;
        var totalExp = expenses.Sum(e => e.Amount);
        var totalInv = investments.Sum(i => i.Amount);
        var goalDeposits = await db.GoalDeposits
            .Where(d => d.UserId == UserId && d.Date.Year == year && d.Date.Month == month)
            .SumAsync(d => (decimal?)d.Amount) ?? 0;
        var balance = totalIncome - totalExp - totalInv - goalDeposits;
        var projectedBalance = balance - monthExp.Pending;
        var pct = totalIncome > 0 ? Math.Round(totalExp / totalIncome * 100, 1) : 0;

        var budgetDto = new BudgetDto(budget?.Id ?? 0, year, month, salary, totalExp, totalInv, balance, pct, otherIncome, totalIncome, goalDeposits,
            totalExp, monthExp.Pending, projectedBalance);


        var catSummaries = expenses
            .GroupBy(e => e.Category)
            .Select(g => new CategorySummaryDto(
                new CategoryDto(g.Key.Id, g.Key.Name, g.Key.Icon, g.Key.Color, g.Key.IsSystem),
                g.Sum(e => e.Amount),
                totalIncome > 0 ? Math.Round(g.Sum(e => e.Amount) / totalIncome * 100, 1) : 0))
            .OrderByDescending(s => s.Total);


        var trend = new List<MonthlyTrendDto>();
        var months = new[] { "Jan", "Fev", "Mar", "Abr", "Mai", "Jun", "Jul", "Ago", "Set", "Out", "Nov", "Dez" };
        for (int i = 5; i >= 0; i--)
        {
            var d = new DateTime(year, month, 1).AddMonths(-i);
            var b2 = await db.MonthlyBudgets.FirstOrDefaultAsync(b => b.UserId == UserId && b.Year == d.Year && b.Month == d.Month);
            var e2 = (await db.MonthExpensesAsync(UserId, d.Year, d.Month)).CountedTotal;
            var v2 = await db.Investments.Where(v => v.UserId == UserId && v.Date.Year == d.Year && v.Date.Month == d.Month).SumAsync(v => (decimal?)v.Amount) ?? 0;
            var r2 = await db.Incomes.Where(r => r.UserId == UserId && r.Date.Year == d.Year && r.Date.Month == d.Month).SumAsync(r => (decimal?)r.Amount) ?? 0;
            var g2 = await db.GoalDeposits.Where(x => x.UserId == UserId && x.Date.Year == d.Year && x.Date.Month == d.Month).SumAsync(x => (decimal?)x.Amount) ?? 0;
            var s2 = b2?.Salary ?? 0;
            trend.Add(new MonthlyTrendDto(d.Year, d.Month, $"{months[d.Month - 1]}/{d.Year % 100:00}", s2, e2, v2, s2 + r2 - e2 - v2 - g2, r2, g2));
        }


        var unread = await db.Alerts.Where(a => a.UserId == UserId && !a.IsRead)
            .OrderByDescending(a => a.CreatedAt).Take(10)
            .Select(a => new AlertDto(a.Id, a.Title, a.Message, a.Type, a.IsRead, a.CreatedAt))
            .ToListAsync();


        var goals = await db.Goals.Where(g => g.UserId == UserId && !g.IsCompleted)
            .OrderByDescending(g => g.CreatedAt).Take(5).ToListAsync();
        var goalDtos = goals.Select(GoalCalculator.ToDto);

        return Ok(new DashboardDto(budgetDto, catSummaries, trend, unread, goalDtos));
    }
}