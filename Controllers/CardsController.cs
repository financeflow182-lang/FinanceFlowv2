using FinancasApi.Data;
using FinancasApi.DTOs;
using FinancasApi.Models;
using FinancasApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinancasApi.Controllers;

// Bancos disponíveis para cadastro de cartões (do sistema + criados pelo usuário)
[Route("api/banks")]
public class BanksController(AppDbContext db) : BaseController
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<BankDto>>> List()
    {
        var banks = await db.Banks
            .Where(b => b.IsSystem || b.UserId == UserId)
            .OrderBy(b => b.Name == "Outro").ThenBy(b => b.Name)
            .ToListAsync();
        return Ok(banks.Select(ToDto));
    }

    [HttpPost]
    public async Task<ActionResult<BankDto>> Create(CreateBankRequest req)
    {
        if (await db.Banks.CountAsync(b => b.UserId == UserId) >= 50)
            return BadRequest(new { message = "Limite de bancos atingido." });

        var name = req.Name.Trim();
        if (await db.Banks.AnyAsync(b => (b.IsSystem || b.UserId == UserId) && b.Name == name))
            return Conflict(new { message = "Já existe um banco com esse nome." });

        var bank = new Bank { Name = name, Icon = "🏦", Color = req.Color, UserId = UserId };
        db.Banks.Add(bank);
        await db.SaveChangesAsync();
        return Ok(ToDto(bank));
    }

    private static BankDto ToDto(Bank b) => new(b.Id, b.Name, b.Icon, b.Color, b.IsSystem);
}


// Cartões de crédito, compras (com parcelamento) e faturas. Guarda só dados de identificação do cartão.
[Route("api/cards")]
public class CardsController(AppDbContext db, AlertService alerts) : BaseController
{
    private const int MaxCards = 30;

    [HttpGet("brands")]
    public ActionResult<IEnumerable<string>> Brands() => Ok(CardBrands.All);

    [HttpGet]
    public async Task<ActionResult<IEnumerable<CardDto>>> List([FromQuery] bool includeArchived = false)
    {
        var q = db.CreditCards.Include(c => c.Bank).Where(c => c.UserId == UserId);
        if (!includeArchived) q = q.Where(c => !c.IsArchived);

        await alerts.CheckCardAlertsAsync(UserId);

        var cards = await q.OrderBy(c => c.Nickname).ToListAsync();

        var result = new List<CardDto>();
        foreach (var c in cards) result.Add(await ToDto(c));
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<CardDto>> Get(int id)
    {
        var card = await FindCard(id);
        return card == null ? NotFound() : Ok(await ToDto(card));
    }

    [HttpPost]
    public async Task<ActionResult<CardDto>> Create(CreateCardRequest req)
    {
        if (await db.CreditCards.CountAsync(c => c.UserId == UserId) >= MaxCards)
            return BadRequest(new { message = "Limite de cartões atingido." });
        if (!await BankAllowed(req.BankId))
            return BadRequest(new { message = "Banco inválido." });

        var card = new CreditCard
        {
            BankId = req.BankId,
            Nickname = req.Nickname.Trim(),
            Brand = req.Brand,
            Last4 = req.Last4,
            Limit = req.Limit,
            ClosingDay = req.ClosingDay,
            DueDay = req.DueDay,
            UserId = UserId
        };
        db.CreditCards.Add(card);
        await db.SaveChangesAsync();

        await db.Entry(card).Reference(c => c.Bank).LoadAsync();
        return CreatedAtAction(nameof(Get), new { id = card.Id }, await ToDto(card));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<CardDto>> Update(int id, UpdateCardRequest req)
    {
        var card = await FindCard(id);
        if (card == null) return NotFound();
        if (!await BankAllowed(req.BankId))
            return BadRequest(new { message = "Banco inválido." });

        card.BankId = req.BankId;
        card.Nickname = req.Nickname.Trim();
        card.Brand = req.Brand;
        card.Last4 = req.Last4;
        card.Limit = req.Limit;
        card.ClosingDay = req.ClosingDay;
        card.DueDay = req.DueDay;
        card.IsArchived = req.IsArchived;
        await db.SaveChangesAsync();

        await db.Entry(card).Reference(c => c.Bank).LoadAsync();
        return Ok(await ToDto(card));
    }

    // Sem compras: exclui. Com compras: arquiva, para não perder o histórico de gastos.
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var card = await FindCard(id);
        if (card == null) return NotFound();

        if (await db.Expenses.AnyAsync(e => e.CreditCardId == id))
        {
            card.IsArchived = true;
            await db.SaveChangesAsync();
            return Ok(new { message = "Cartão arquivado, pois possui compras registradas.", archived = true });
        }

        db.CreditCards.Remove(card);
        await db.SaveChangesAsync();
        return NoContent();
    }


    // ---- Compras ----

    // Cria a compra; se parcelada, uma despesa por parcela (cada uma na sua fatura e no seu mês)
    [HttpPost("{id}/purchases")]
    public async Task<ActionResult<IEnumerable<ExpenseDto>>> Purchase(int id, CreateCardPurchaseRequest req)
    {
        var card = await FindCard(id);
        if (card == null) return NotFound();
        if (card.IsArchived)
            return BadRequest(new { message = "Este cartão está arquivado." });

        if (req.IsRecurring && req.Installments > 1)
            return BadRequest(new { message = "Uma compra recorrente não pode ser parcelada." });

        var category = await db.Categories
            .FirstOrDefaultAsync(c => c.Id == req.CategoryId && (c.IsSystem || c.UserId == UserId));
        if (category == null)
            return BadRequest(new { message = "Categoria inválida." });

        var amounts = InvoiceCalculator.SplitInstallments(req.Amount, req.Installments);
        var (invYear, invMonth) = InvoiceCalculator.InvoiceFor(req.Date, card.ClosingDay);
        var firstInvoice = new DateOnly(invYear, invMonth, 1);
        var groupId = req.Installments > 1 ? Guid.NewGuid() : (Guid?)null;

        var expenses = new List<Expense>();
        for (var i = 0; i < req.Installments; i++)
        {
            var invoice = firstInvoice.AddMonths(i);
            expenses.Add(new Expense
            {
                Description = req.Installments > 1
                    ? $"{req.Description.Trim()} ({i + 1}/{req.Installments})"
                    : req.Description.Trim(),
                Amount = amounts[i],
                Date = req.Date.AddMonths(i),
                CategoryId = category.Id,
                Category = category,
                IsRecurring = req.IsRecurring,
                UserId = UserId,
                CreditCardId = card.Id,
                InvoiceYear = invoice.Year,
                InvoiceMonth = invoice.Month,
                InstallmentGroupId = groupId,
                InstallmentNumber = req.Installments > 1 ? i + 1 : null,
                InstallmentTotal = req.Installments > 1 ? req.Installments : null
            });
        }

        db.Expenses.AddRange(expenses);
        await db.SaveChangesAsync();

        await alerts.CheckCardAlertsAsync(UserId);
        return Ok(expenses.Select(e => ToExpenseDto(e)));
    }

    // Exclui todas as parcelas de uma compra parcelada
    [HttpDelete("{id}/purchases/{groupId:guid}")]
    public async Task<IActionResult> DeletePurchaseGroup(int id, Guid groupId)
    {
        var items = await db.Expenses
            .Where(e => e.UserId == UserId && e.CreditCardId == id && e.InstallmentGroupId == groupId)
            .ToListAsync();
        if (items.Count == 0) return NotFound();

        db.Expenses.RemoveRange(items);
        await db.SaveChangesAsync();
        return NoContent();
    }


    // ---- Saldos de fatura já existentes ----

    [HttpGet("{id}/invoice-balances")]
    public async Task<ActionResult<IEnumerable<InvoiceBalanceDto>>> GetInvoiceBalances(int id)
    {
        if (await FindCard(id) == null) return NotFound();

        var list = await db.Expenses
            .Where(e => e.UserId == UserId && e.CreditCardId == id && e.IsInvoiceBalance)
            .OrderBy(e => e.InvoiceYear).ThenBy(e => e.InvoiceMonth)
            .ToListAsync();
        return Ok(list.Select(ToBalanceDto));
    }

    // Define de uma vez o valor já comprometido de cada fatura (ex.: parcelas até 2027).
    // Reenviar um mês atualiza o valor; valor 0 remove.
    [HttpPut("{id}/invoice-balances")]
    public async Task<ActionResult<IEnumerable<InvoiceBalanceDto>>> SetInvoiceBalances(int id, SetInvoiceBalancesRequest req)
    {
        var card = await FindCard(id);
        if (card == null) return NotFound();
        if (card.IsArchived)
            return BadRequest(new { message = "Este cartão está arquivado." });

        if (req.Items.GroupBy(i => (i.Year, i.Month)).Any(g => g.Count() > 1))
            return BadRequest(new { message = "Há faturas repetidas na lista." });

        var other = await db.Categories.FirstOrDefaultAsync(c => c.IsSystem && c.Name == "Outros");
        if (other == null)
            return BadRequest(new { message = "Categoria padrão não encontrada." });

        var existing = await db.Expenses
            .Where(e => e.UserId == UserId && e.CreditCardId == id && e.IsInvoiceBalance)
            .ToListAsync();

        foreach (var item in req.Items)
        {
            var current = existing.FirstOrDefault(e => e.InvoiceYear == item.Year && e.InvoiceMonth == item.Month);

            if (item.Amount == 0)
            {
                if (current != null) db.Expenses.Remove(current);
                continue;
            }

            if (current == null)
            {
                db.Expenses.Add(new Expense
                {
                    Description = $"Parcelas já existentes ({item.Month:00}/{item.Year})",
                    Amount = item.Amount,
                    Date = new DateOnly(item.Year, item.Month, 1),
                    CategoryId = other.Id,
                    UserId = UserId,
                    CreditCardId = id,
                    InvoiceYear = item.Year,
                    InvoiceMonth = item.Month,
                    IsInvoiceBalance = true,
                    ExcludeFromBudget = !item.CountInBudget
                });
            }
            else
            {
                current.Amount = item.Amount;
                current.ExcludeFromBudget = !item.CountInBudget;
            }
        }

        await db.SaveChangesAsync();
        return await GetInvoiceBalances(id);
    }


    // ---- Faturas ----

    // Fatura identificada pelo mês de fechamento
    [HttpGet("{id}/invoices/{year:int}/{month:int}")]
    public async Task<ActionResult<InvoiceDto>> Invoice(int id, int year, int month)
    {
        if (year is < 2000 or > 2100 || month is < 1 or > 12)
            return BadRequest(new { message = "Fatura inválida." });

        var card = await FindCard(id);
        if (card == null) return NotFound();

        var items = await InvoiceItems(card.Id, year, month)
            .Include(e => e.Category)
            .OrderBy(e => e.Date).ThenBy(e => e.CreatedAt)
            .ToListAsync();

        var payment = await db.InvoicePayments
            .FirstOrDefaultAsync(p => p.CreditCardId == card.Id && p.Year == year && p.Month == month);

        var closing = InvoiceCalculator.ClosingDate(year, month, card.ClosingDay);
        var due = InvoiceCalculator.DueDate(year, month, card.ClosingDay, card.DueDay);
        var status = InvoiceCalculator.Status(InvoiceCalculator.Today(), closing, due, payment != null);

        return Ok(new InvoiceDto(card.Id, year, month, closing, due,
            items.Sum(e => e.Amount), status, payment?.PaidAt,
            items.Select(e => ToExpenseDto(e, payment != null, payment?.PaidAt))));
    }

    [HttpPost("{id}/invoices/{year:int}/{month:int}/pay")]
    public async Task<ActionResult<InvoiceDto>> Pay(int id, int year, int month)
    {
        if (year is < 2000 or > 2100 || month is < 1 or > 12)
            return BadRequest(new { message = "Fatura inválida." });

        var card = await FindCard(id);
        if (card == null) return NotFound();

        if (await db.InvoicePayments.AnyAsync(p => p.CreditCardId == card.Id && p.Year == year && p.Month == month))
            return Conflict(new { message = "Esta fatura já está marcada como paga." });

        var total = await InvoiceItems(card.Id, year, month).SumAsync(e => (decimal?)e.Amount) ?? 0;
        db.InvoicePayments.Add(new InvoicePayment
        {
            CreditCardId = card.Id, UserId = UserId, Year = year, Month = month, Amount = total
        });

        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException) { return Conflict(new { message = "Esta fatura já está marcada como paga." }); }

        return await Invoice(id, year, month);
    }

    [HttpDelete("{id}/invoices/{year:int}/{month:int}/pay")]
    public async Task<ActionResult<InvoiceDto>> Unpay(int id, int year, int month)
    {
        var card = await FindCard(id);
        if (card == null) return NotFound();

        var payment = await db.InvoicePayments
            .FirstOrDefaultAsync(p => p.CreditCardId == card.Id && p.Year == year && p.Month == month);
        if (payment == null)
            return NotFound(new { message = "Esta fatura não está marcada como paga." });

        db.InvoicePayments.Remove(payment);
        await db.SaveChangesAsync();
        return await Invoice(id, year, month);
    }


    // ---- Auxiliares ----

    private Task<CreditCard?> FindCard(int id) =>
        db.CreditCards.Include(c => c.Bank).FirstOrDefaultAsync(c => c.Id == id && c.UserId == UserId);

    // Só bancos do sistema ou do próprio usuário (evita IDOR)
    private Task<bool> BankAllowed(int bankId) =>
        db.Banks.AnyAsync(b => b.Id == bankId && (b.IsSystem || b.UserId == UserId));

    private IQueryable<Expense> InvoiceItems(int cardId, int year, int month) =>
        db.InvoiceItems(UserId, cardId, year, month);

    private async Task<CardDto> ToDto(CreditCard c)
    {
        var used = await db.UsedLimitAsync(UserId, c.Id);
        var cur = await db.CurrentInvoiceAsync(UserId, c);

        return new CardDto(
            c.Id, c.BankId, c.Bank.Name, c.Bank.Color, c.Bank.Icon,
            c.Nickname, c.Brand, c.Last4,
            c.Limit, c.ClosingDay, c.DueDay, c.IsArchived,
            used, c.Limit - used,
            cur.Year, cur.Month, cur.Total, cur.ClosingDate, cur.DueDate, cur.Status);
    }

    private static InvoiceBalanceDto ToBalanceDto(Expense e) =>
        new(e.Id, e.InvoiceYear!.Value, e.InvoiceMonth!.Value, e.Amount, !e.ExcludeFromBudget);

    private static ExpenseDto ToExpenseDto(Expense e, bool isPaid = false, DateTime? paidAt = null) => new(
        e.Id, e.Description, e.Amount, e.Date,
        new CategoryDto(e.Category.Id, e.Category.Name, e.Category.Icon, e.Category.Color, e.Category.IsSystem),
        e.IsRecurring, e.CreatedAt, e.CreditCardId, e.InstallmentNumber, e.InstallmentTotal, e.InstallmentGroupId,
        e.IsInvoiceBalance, e.ExcludeFromBudget, isPaid, paidAt);
}
