using FinancasApi.Data;
using FinancasApi.DTOs;
using FinancasApi.Models;
using FinancasApi.Services;
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
    public async Task<ActionResult<IEnumerable<InvestmentDto>>> List([FromQuery] int? year, [FromQuery] int? month)
    {
        var q = db.Investments.Where(i => i.UserId == UserId);
        if (year.HasValue) q = q.Where(i => i.Date.Year == year.Value);
        if (month.HasValue) q = q.Where(i => i.Date.Month == month.Value);
        var list = await q.OrderByDescending(i => i.Date).ToListAsync();
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
        var list = await db.Goals.Where(g => g.UserId == UserId).OrderByDescending(g => g.CreatedAt).ToListAsync();
        return Ok(list.Select(ToDto));
    }

    [HttpPost]
    public async Task<ActionResult<GoalDto>> Create(CreateGoalRequest req)
    {
        var goal = new Goal { Name = req.Name, Icon = req.Icon, TargetAmount = req.TargetAmount, Deadline = req.Deadline, UserId = UserId };
        db.Goals.Add(goal);
        await db.SaveChangesAsync();
        return Ok(ToDto(goal));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<GoalDto>> Update(int id, UpdateGoalRequest req)
    {
        var goal = await db.Goals.FirstOrDefaultAsync(g => g.Id == id && g.UserId == UserId);
        if (goal == null) return NotFound();
        goal.Name = req.Name; goal.Icon = req.Icon; goal.TargetAmount = req.TargetAmount; goal.Deadline = req.Deadline;
        await db.SaveChangesAsync();
        return Ok(ToDto(goal));
    }

    [HttpPost("{id}/deposit")]
    public async Task<ActionResult<GoalDto>> Deposit(int id, AddToGoalRequest req)
    {
        var goal = await db.Goals.FirstOrDefaultAsync(g => g.Id == id && g.UserId == UserId);
        if (goal == null) return NotFound();
        goal.CurrentAmount += req.Amount;
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

    private static GoalDto ToDto(Goal g) => new(
        g.Id, g.Name, g.Icon, g.TargetAmount, g.CurrentAmount, g.Deadline, g.IsCompleted,
        g.TargetAmount > 0 ? Math.Round(g.CurrentAmount / g.TargetAmount * 100, 1) : 0,
        g.CreatedAt);
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
public class DashboardController(AppDbContext db) : BaseController
{
    [HttpGet("{year}/{month}")]
    public async Task<ActionResult<DashboardDto>> Get(int year, int month)
    {
        // Budget
        var budget = await db.MonthlyBudgets
            .FirstOrDefaultAsync(b => b.UserId == UserId && b.Year == year && b.Month == month);
        var salary = budget?.Salary ?? 0;

        var expenses = await db.Expenses.Include(e => e.Category)
            .Where(e => e.UserId == UserId && e.Date.Year == year && e.Date.Month == month)
            .ToListAsync();
        var investments = await db.Investments
            .Where(i => i.UserId == UserId && i.Date.Year == year && i.Date.Month == month)
            .ToListAsync();

        var totalExp = expenses.Sum(e => e.Amount);
        var totalInv = investments.Sum(i => i.Amount);
        var balance = salary - totalExp - totalInv;
        var pct = salary > 0 ? Math.Round(totalExp / salary * 100, 1) : 0;

        var budgetDto = new BudgetDto(budget?.Id ?? 0, year, month, salary, totalExp, totalInv, balance, pct);


        var catSummaries = expenses
            .GroupBy(e => e.Category)
            .Select(g => new CategorySummaryDto(
                new CategoryDto(g.Key.Id, g.Key.Name, g.Key.Icon, g.Key.Color, g.Key.IsSystem),
                g.Sum(e => e.Amount),
                salary > 0 ? Math.Round(g.Sum(e => e.Amount) / salary * 100, 1) : 0))
            .OrderByDescending(s => s.Total);


        var trend = new List<MonthlyTrendDto>();
        var months = new[] { "Jan", "Fev", "Mar", "Abr", "Mai", "Jun", "Jul", "Ago", "Set", "Out", "Nov", "Dez" };
        for (int i = 5; i >= 0; i--)
        {
            var d = new DateTime(year, month, 1).AddMonths(-i);
            var b2 = await db.MonthlyBudgets.FirstOrDefaultAsync(b => b.UserId == UserId && b.Year == d.Year && b.Month == d.Month);
            var e2 = await db.Expenses.Where(e => e.UserId == UserId && e.Date.Year == d.Year && e.Date.Month == d.Month).SumAsync(e => (decimal?)e.Amount) ?? 0;
            var v2 = await db.Investments.Where(v => v.UserId == UserId && v.Date.Year == d.Year && v.Date.Month == d.Month).SumAsync(v => (decimal?)v.Amount) ?? 0;
            var s2 = b2?.Salary ?? 0;
            trend.Add(new MonthlyTrendDto(d.Year, d.Month, $"{months[d.Month - 1]}/{d.Year % 100:00}", s2, e2, v2, s2 - e2 - v2));
        }


        var unread = await db.Alerts.Where(a => a.UserId == UserId && !a.IsRead)
            .OrderByDescending(a => a.CreatedAt).Take(10)
            .Select(a => new AlertDto(a.Id, a.Title, a.Message, a.Type, a.IsRead, a.CreatedAt))
            .ToListAsync();


        var goals = await db.Goals.Where(g => g.UserId == UserId && !g.IsCompleted)
            .OrderByDescending(g => g.CreatedAt).Take(5).ToListAsync();
        var goalDtos = goals.Select(g => new GoalDto(
            g.Id, g.Name, g.Icon, g.TargetAmount, g.CurrentAmount, g.Deadline, g.IsCompleted,
            g.TargetAmount > 0 ? Math.Round(g.CurrentAmount / g.TargetAmount * 100, 1) : 0,
            g.CreatedAt));

        return Ok(new DashboardDto(budgetDto, catSummaries, trend, unread, goalDtos));
    }
}