using FinancasApi.Data;
using FinancasApi.DTOs;
using FinancasApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinancasApi.Controllers;


// Criação do orçamento mensal, com cálculo do saldo e porcentagem gasto
[Route("api/budgets")]
public class BudgetsController(AppDbContext db) : BaseController
{
    [HttpGet("{year}/{month}")]
    public async Task<ActionResult<BudgetDto>> Get(int year, int month)
    {
        var budget = await db.MonthlyBudgets
            .FirstOrDefaultAsync(b => b.UserId == UserId && b.Year == year && b.Month == month);

        return Ok(await BuildDto(budget, year, month));
    }

    [HttpPut]
    public async Task<ActionResult<BudgetDto>> Upsert(UpsertBudgetRequest req)
    {
        var budget = await db.MonthlyBudgets
            .FirstOrDefaultAsync(b => b.UserId == UserId && b.Year == req.Year && b.Month == req.Month);

        if (budget == null)
        {
            budget = new MonthlyBudget { UserId = UserId, Year = req.Year, Month = req.Month };
            db.MonthlyBudgets.Add(budget);
        }

        budget.Salary = req.Salary;
        await db.SaveChangesAsync();
        return Ok(await BuildDto(budget, req.Year, req.Month));
    }

    private async Task<BudgetDto> BuildDto(MonthlyBudget? b, int year, int month)
    {
        var salary = b?.Salary ?? 0;
        var expenses = await db.Expenses
            .Where(e => e.UserId == UserId && e.Date.Year == year && e.Date.Month == month)
            .SumAsync(e => (decimal?)e.Amount) ?? 0;
        var investments = await db.Investments
            .Where(i => i.UserId == UserId && i.Date.Year == year && i.Date.Month == month)
            .SumAsync(i => (decimal?)i.Amount) ?? 0;
        var balance = salary - expenses - investments;
        var pct = salary > 0 ? Math.Round(expenses / salary * 100, 1) : 0;

        return new BudgetDto(b?.Id ?? 0, year, month, salary, expenses, investments, balance, pct);
    }
}