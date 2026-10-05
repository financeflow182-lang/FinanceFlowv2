using FinancasApi.Data;
using FinancasApi.DTOs;
using FinancasApi.Models;
using FinancasApi.Services;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinancasApi.Controllers;

// Criação das despesas, com relação com categorias e alertas
[Route("api/expenses")]
public class ExpensesController(AppDbContext db, AlertService alerts) : BaseController
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ExpenseDto>>> List(
        [FromQuery, Range(2000, 2100)] int? year, [FromQuery, Range(1, 12)] int? month, [FromQuery] int? categoryId)
    {
        var q = db.Expenses
            .Include(e => e.Category)
            .Where(e => e.UserId == UserId && !e.ExcludeFromBudget);

        if (year.HasValue && month.HasValue)
        {
            var inicioMes = new DateOnly(year.Value, month.Value, 1);
            var fimMes = inicioMes.AddMonths(1).AddDays(-1);

            q = q.Where(e =>
                // gastos do mês
                (e.Date >= inicioMes && e.Date <= fimMes)

                // gastos recorrentes que se aplicam ao mês
                || (e.IsRecurring && e.Date <= fimMes)
            );
        }
        if (categoryId.HasValue)
            q = q.Where(e => e.CategoryId == categoryId.Value);

        var list = await q
            .OrderByDescending(e => e.Date)
            .ThenByDescending(e => e.CreatedAt)
            .Take(1000)
            .ToListAsync();

        var paid = await db.PaidMapAsync(UserId, list, year, month);
        return Ok(list.Select(e => ToDto(e, year, month, paid)));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ExpenseDto>> Get(int id)
    {
        var e = await db.Expenses
            .Include(e => e.Category)
            .FirstOrDefaultAsync(e => e.Id == id && e.UserId == UserId);

        if (e == null) return NotFound();
        return Ok(ToDto(e, null, null, await db.PaidMapAsync(UserId, [e], null, null)));
    }

    [HttpPost]
    public async Task<ActionResult<ExpenseDto>> Create(CreateExpenseRequest req)
    {
        if (!await CategoryAllowed(req.CategoryId))
            return BadRequest(new { message = "Categoria inválida." });

        var expense = new Expense
        {
            Description = req.Description,
            Amount = req.Amount,
            Date = req.Date,
            CategoryId = req.CategoryId,
            IsRecurring = req.IsRecurring,
            IsPaid = req.IsPaid && !req.IsRecurring,
            PaidAt = req.IsPaid && !req.IsRecurring ? DateTime.UtcNow : null,
            UserId = UserId
        };

        db.Expenses.Add(expense);
        await db.SaveChangesAsync();

        await alerts.CheckAndCreateAlertsAsync(UserId, req.Date.Year, req.Date.Month);

        await db.Entry(expense).Reference(e => e.Category).LoadAsync();

        return CreatedAtAction(nameof(Get), new { id = expense.Id }, ToDto(expense, null, null, await db.PaidMapAsync(UserId, [expense], null, null)));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ExpenseDto>> Update(int id, UpdateExpenseRequest req)
    {
        var expense = await db.Expenses
            .Include(e => e.Category)
            .FirstOrDefaultAsync(e => e.Id == id && e.UserId == UserId);

        if (expense == null) return NotFound();

        if (!await CategoryAllowed(req.CategoryId))
            return BadRequest(new { message = "Categoria inválida." });

        expense.Description = req.Description;
        expense.Amount = req.Amount;
        expense.Date = req.Date;
        expense.CategoryId = req.CategoryId;
        expense.IsRecurring = req.IsRecurring;

        await db.SaveChangesAsync();

        await alerts.CheckAndCreateAlertsAsync(UserId, req.Date.Year, req.Date.Month);

        await db.Entry(expense).Reference(e => e.Category).LoadAsync();

        return Ok(ToDto(expense, null, null, await db.PaidMapAsync(UserId, [expense], null, null)));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var expense = await db.Expenses
            .FirstOrDefaultAsync(e => e.Id == id && e.UserId == UserId);

        if (expense == null) return NotFound();

        db.Expenses.Remove(expense);
        await db.SaveChangesAsync();

        return NoContent();
    }

    // Só categorias do sistema ou do próprio usuário (evita IDOR)
    private Task<bool> CategoryAllowed(int categoryId) =>
        db.Categories.AnyAsync(c => c.Id == categoryId && (c.IsSystem || c.UserId == UserId));

    // Marca a despesa como paga. Recorrentes são pagas por mês (year e month obrigatórios); cartão é pago pela fatura.
    [HttpPost("{id}/pay")]
    public async Task<ActionResult<ExpenseDto>> Pay(int id, [FromQuery] int? year, [FromQuery] int? month)
    {
        var expense = await db.Expenses.Include(e => e.Category)
            .FirstOrDefaultAsync(e => e.Id == id && e.UserId == UserId);
        if (expense == null) return NotFound();

        var error = ValidatePayable(expense, year, month);
        if (error != null) return BadRequest(new { message = error });

        if (expense.IsRecurring)
        {
            var exists = await db.ExpensePayments.AnyAsync(p =>
                p.ExpenseId == id && p.Year == year!.Value && p.Month == month!.Value);
            if (!exists)
            {
                db.ExpensePayments.Add(new ExpensePayment { ExpenseId = id, UserId = UserId, Year = year!.Value, Month = month!.Value });
                try { await db.SaveChangesAsync(); } catch (DbUpdateException) { /* pagamento simultâneo: já registrado */ }
            }
        }
        else if (!expense.IsPaid)
        {
            expense.IsPaid = true;
            expense.PaidAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        return Ok(ToDto(expense, year, month, await db.PaidMapAsync(UserId, [expense], year, month)));
    }

    [HttpDelete("{id}/pay")]
    public async Task<ActionResult<ExpenseDto>> Unpay(int id, [FromQuery] int? year, [FromQuery] int? month)
    {
        var expense = await db.Expenses.Include(e => e.Category)
            .FirstOrDefaultAsync(e => e.Id == id && e.UserId == UserId);
        if (expense == null) return NotFound();

        var error = ValidatePayable(expense, year, month);
        if (error != null) return BadRequest(new { message = error });

        if (expense.IsRecurring)
        {
            await db.ExpensePayments
                .Where(p => p.ExpenseId == id && p.Year == year!.Value && p.Month == month!.Value)
                .ExecuteDeleteAsync();
        }
        else if (expense.IsPaid)
        {
            expense.IsPaid = false;
            expense.PaidAt = null;
            await db.SaveChangesAsync();
        }

        return Ok(ToDto(expense, year, month, await db.PaidMapAsync(UserId, [expense], year, month)));
    }

    private static string? ValidatePayable(Expense e, int? year, int? month)
    {
        if (e.CreditCardId != null)
            return "Compras no cartão são pagas pela fatura do cartão.";
        if (!e.IsRecurring) return null;
        if (year is null or < 2000 or > 2100 || month is null or < 1 or > 12)
            return "Informe o ano e o mês do pagamento para despesas recorrentes.";
        if (e.Date > new DateOnly(year.Value, month.Value, 1).AddMonths(1).AddDays(-1))
            return "Esta despesa recorrente ainda não começou nesse mês.";
        return null;
    }

    private static ExpenseDto ToDto(Expense e, int? year, int? month, Dictionary<int, DateTime?> paid)
    {
        var date = e.Date;

        if (e.IsRecurring && year.HasValue && month.HasValue)
        {
            var day = Math.Min(e.Date.Day, DateTime.DaysInMonth(year.Value, month.Value));
            date = new DateOnly(year.Value, month.Value, day);
        }

        return new ExpenseDto(
            e.Id,
            e.Description,
            e.Amount,
            date,
            new CategoryDto(
                e.Category.Id,
                e.Category.Name,
                e.Category.Icon,
                e.Category.Color,
                e.Category.IsSystem
            ),
            e.IsRecurring,
            e.CreatedAt,
            e.CreditCardId,
            e.InstallmentNumber,
            e.InstallmentTotal,
            e.InstallmentGroupId,
            e.IsInvoiceBalance,
            e.ExcludeFromBudget,
            paid.ContainsKey(e.Id),
            paid.GetValueOrDefault(e.Id)
        );
    }
}