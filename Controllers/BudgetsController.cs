using FinancasApi.Data;
using FinancasApi.DTOs;
using FinancasApi.Models;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinancasApi.Controllers;


// Criação do orçamento mensal, com cálculo do saldo e porcentagem gasto
[Route("api/budgets")]
public class BudgetsController(AppDbContext db) : BaseController
{
    [HttpGet("{year}/{month}")]
    public async Task<ActionResult<BudgetDto>> Get([Range(2000, 2100)] int year, [Range(1, 12)] int month)
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
            .ForMonth(UserId, year, month)
            .SumAsync(e => (decimal?)e.Amount) ?? 0;
        var investments = await db.Investments
            .Where(i => i.UserId == UserId && i.Date.Year == year && i.Date.Month == month)
            .SumAsync(i => (decimal?)i.Amount) ?? 0;
        var otherIncome = await db.Incomes
            .Where(i => i.UserId == UserId && i.Date.Year == year && i.Date.Month == month)
            .SumAsync(i => (decimal?)i.Amount) ?? 0;
        var totalIncome = salary + otherIncome;
        var balance = totalIncome - expenses - investments;
        var pct = totalIncome > 0 ? Math.Round(expenses / totalIncome * 100, 1) : 0;

        return new BudgetDto(b?.Id ?? 0, year, month, salary, expenses, investments, balance, pct, otherIncome, totalIncome);
    }
}