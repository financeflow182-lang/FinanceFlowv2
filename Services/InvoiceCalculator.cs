namespace FinancasApi.Services;

// Regras de fatura de cartão: a que fatura uma compra pertence e quando ela fecha e vence
public static class InvoiceCalculator
{
    private static int Clamp(int year, int month, int day) => Math.Min(day, DateTime.DaysInMonth(year, month));

    // Fatura identificada pelo mês de fechamento. A fatura fecha no próprio dia de fechamento:
    // compra antes desse dia entra na fatura do mês; do dia de fechamento em diante, na do mês seguinte.
    public static (int Year, int Month) InvoiceFor(DateOnly purchase, int closingDay)
    {
        var closing = Clamp(purchase.Year, purchase.Month, closingDay);
        var d = purchase.Day < closing ? purchase : purchase.AddMonths(1);
        return (d.Year, d.Month);
    }

    // "paid" (paga) | "overdue" (atrasada: passou o vencimento sem pagar) | "closed" (fechada, a pagar) | "open" (aberta)
    public static string Status(DateOnly today, DateOnly closing, DateOnly due, bool paid) =>
        paid ? "paid"
        : today > due ? "overdue"
        : today >= closing ? "closed"
        : "open";

    public static DateOnly ClosingDate(int year, int month, int closingDay) =>
        new(year, month, Clamp(year, month, closingDay));

    // Vencimento no mesmo mês se o dia for depois do fechamento, senão no mês seguinte
    public static DateOnly DueDate(int year, int month, int closingDay, int dueDay)
    {
        var due = dueDay > closingDay ? new DateOnly(year, month, 1) : new DateOnly(year, month, 1).AddMonths(1);
        return new DateOnly(due.Year, due.Month, Clamp(due.Year, due.Month, dueDay));
    }

    // Divide o total em parcelas em centavos; a diferença de arredondamento vai na última
    public static decimal[] SplitInstallments(decimal total, int count)
    {
        var baseValue = Math.Floor(total / count * 100) / 100;
        var parts = Enumerable.Repeat(baseValue, count).ToArray();
        parts[^1] = total - baseValue * (count - 1);
        return parts;
    }

    // Hoje no horário de Brasília
    public static DateOnly Today() => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-3));
}
