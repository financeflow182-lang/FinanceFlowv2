using FinancasApi.Data;
using FinancasApi.DTOs;
using FinancasApi.Models;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinancasApi.Controllers;

// Receitas além do salário: freelas, vendas, 13º, reembolsos e rendimentos
[Route("api/incomes")]
public class IncomesController(AppDbContext db) : BaseController
{
    [HttpGet("categories")]
    public ActionResult<IEnumerable<string>> Categories() => Ok(IncomeCategories.All);

    [HttpGet]
    public async Task<ActionResult<IEnumerable<IncomeDto>>> List(
        [FromQuery, Range(2000, 2100)] int? year, [FromQuery, Range(1, 12)] int? month, [FromQuery] string? category)
    {
        var q = db.Incomes.Where(i => i.UserId == UserId);
        if (year.HasValue) q = q.Where(i => i.Date.Year == year.Value);
        if (month.HasValue) q = q.Where(i => i.Date.Month == month.Value);
        if (!string.IsNullOrWhiteSpace(category)) q = q.Where(i => i.Category == category);

        var list = await q
            .OrderByDescending(i => i.Date)
            .ThenByDescending(i => i.CreatedAt)
            .Take(1000)
            .ToListAsync();
        return Ok(list.Select(ToDto));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<IncomeDto>> Get(int id)
    {
        var i = await db.Incomes.FirstOrDefaultAsync(i => i.Id == id && i.UserId == UserId);
        return i == null ? NotFound() : Ok(ToDto(i));
    }

    [HttpPost]
    public async Task<ActionResult<IncomeDto>> Create(CreateIncomeRequest req)
    {
        var income = new Income
        {
            Description = req.Description.Trim(),
            Category = req.Category,
            Amount = req.Amount,
            Date = req.Date,
            UserId = UserId
        };
        db.Incomes.Add(income);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = income.Id }, ToDto(income));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<IncomeDto>> Update(int id, UpdateIncomeRequest req)
    {
        var income = await db.Incomes.FirstOrDefaultAsync(i => i.Id == id && i.UserId == UserId);
        if (income == null) return NotFound();

        income.Description = req.Description.Trim();
        income.Category = req.Category;
        income.Amount = req.Amount;
        income.Date = req.Date;
        await db.SaveChangesAsync();
        return Ok(ToDto(income));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var income = await db.Incomes.FirstOrDefaultAsync(i => i.Id == id && i.UserId == UserId);
        if (income == null) return NotFound();

        db.Incomes.Remove(income);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static IncomeDto ToDto(Income i) =>
        new(i.Id, i.Description, i.Category, i.Amount, i.Date, i.CreatedAt);
}
