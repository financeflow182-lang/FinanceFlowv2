using FinancasApi.Models;

namespace FinancasApi.Data;

public static class ExpenseQueryExtensions
{
    // Gastos que contam em um mês: os lançados nele + os recorrentes iniciados até o fim dele
    public static IQueryable<Expense> ForMonth(this IQueryable<Expense> q, int userId, int year, int month)
    {
        var inicio = new DateOnly(year, month, 1);
        var fim = inicio.AddMonths(1).AddDays(-1);
        return q.Where(e => e.UserId == userId &&
            ((e.Date >= inicio && e.Date <= fim) || (e.IsRecurring && e.Date <= fim)));
    }
}
