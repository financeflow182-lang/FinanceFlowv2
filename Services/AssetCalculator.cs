using FinancasApi.Data;
using FinancasApi.DTOs;
using FinancasApi.Models;
using Microsoft.EntityFrameworkCore;

namespace FinancasApi.Services;

// Valor atual e totais do patrimônio
public static class AssetCalculator
{
    // Bens: o valor inicial perde AnnualDepreciationPercent por ano (composto, desde a aquisição); aportes entram pelo valor cheio
    public static decimal CurrentValue(Asset a, decimal contributions, DateOnly today)
    {
        var initial = a.InitialValue;
        if (a.Kind == AssetTypes.Depreciable && a.AnnualDepreciationPercent is { } pct && a.AcquisitionDate < today)
        {
            var years = (double)(today.DayNumber - a.AcquisitionDate.DayNumber) / 365.25;
            initial = (decimal)((double)initial * Math.Pow(1 - (double)pct / 100, years));
        }
        return Math.Round(initial + contributions, 2);
    }

    public static AssetDto ToDto(Asset a, decimal contributions, DateOnly today) => new(
        a.Id, a.Name, a.Kind, a.Type, a.InitialValue, a.AcquisitionDate, a.AnnualDepreciationPercent, a.IsArchived,
        contributions, CurrentValue(a, contributions, today), a.CreatedAt);

    public static async Task<List<AssetDto>> ListAsync(AppDbContext db, int userId, bool includeArchived)
    {
        var today = InvoiceCalculator.Today();
        var q = db.Assets.Where(a => a.UserId == userId);
        if (!includeArchived) q = q.Where(a => !a.IsArchived);
        var assets = await q.OrderBy(a => a.Kind).ThenBy(a => a.Name).ToListAsync();

        var contributions = (await db.Investments
                .Where(i => i.UserId == userId && i.AssetId != null && i.Date <= today)
                .GroupBy(i => i.AssetId!.Value)
                .Select(g => new { g.Key, Total = g.Sum(i => i.Amount) })
                .ToListAsync())
            .ToDictionary(x => x.Key, x => x.Total);

        return assets.Select(a => ToDto(a, contributions.GetValueOrDefault(a.Id), today)).ToList();
    }

    // Aportes sem item vinculado (lançados antes do patrimônio por itens) contam como investidos
    public static async Task<decimal> UnlinkedInvestedAsync(AppDbContext db, int userId)
    {
        var today = InvoiceCalculator.Today();
        return await db.Investments
            .Where(i => i.UserId == userId && i.AssetId == null && i.Date <= today)
            .SumAsync(i => (decimal?)i.Amount) ?? 0;
    }

    public static async Task<decimal> TotalPatrimonyAsync(AppDbContext db, int userId)
    {
        var assets = await ListAsync(db, userId, includeArchived: false);
        return assets.Sum(a => a.CurrentValue) + await UnlinkedInvestedAsync(db, userId);
    }
}
