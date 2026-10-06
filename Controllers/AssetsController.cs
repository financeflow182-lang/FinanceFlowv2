using FinancasApi.Data;
using FinancasApi.DTOs;
using FinancasApi.Models;
using FinancasApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinancasApi.Controllers;

// Patrimônio por itens: investimentos (CDB, Tesouro...) e bens que perdem valor (carro, imóvel...)
[Route("api/assets")]
public class AssetsController(AppDbContext db) : BaseController
{
    private const int MaxAssets = 200;

    [HttpGet("types")]
    public ActionResult<AssetTypesDto> Types() =>
        Ok(new AssetTypesDto(AssetTypes.InvestmentTypes, AssetTypes.DepreciableTypes));

    [HttpGet]
    public async Task<ActionResult<IEnumerable<AssetDto>>> List([FromQuery] bool includeArchived = false) =>
        Ok(await AssetCalculator.ListAsync(db, UserId, includeArchived));

    [HttpGet("{id}")]
    public async Task<ActionResult<AssetDto>> Get(int id)
    {
        var all = await AssetCalculator.ListAsync(db, UserId, includeArchived: true);
        var dto = all.FirstOrDefault(a => a.Id == id);
        return dto == null ? NotFound() : Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<AssetDto>> Create(CreateAssetRequest req)
    {
        if (!AssetTypes.IsValid(req.Kind, req.Type))
            return BadRequest(new { message = TypeError(req.Kind) });
        if (await db.Assets.CountAsync(a => a.UserId == UserId) >= MaxAssets)
            return BadRequest(new { message = "Limite de itens de patrimônio atingido." });

        var asset = new Asset
        {
            Name = req.Name.Trim(),
            Kind = req.Kind,
            Type = req.Type,
            InitialValue = req.InitialValue,
            AcquisitionDate = req.AcquisitionDate,
            AnnualDepreciationPercent = req.Kind == AssetTypes.Depreciable ? req.AnnualDepreciationPercent : null,
            UserId = UserId
        };
        db.Assets.Add(asset);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = asset.Id }, AssetCalculator.ToDto(asset, 0, InvoiceCalculator.Today()));
    }

    // O tipo de patrimônio (investimento ou bem) não muda depois de criado
    [HttpPut("{id}")]
    public async Task<ActionResult<AssetDto>> Update(int id, UpdateAssetRequest req)
    {
        var asset = await db.Assets.FirstOrDefaultAsync(a => a.Id == id && a.UserId == UserId);
        if (asset == null) return NotFound();
        if (!AssetTypes.IsValid(asset.Kind, req.Type))
            return BadRequest(new { message = TypeError(asset.Kind) });

        asset.Name = req.Name.Trim();
        asset.Type = req.Type;
        asset.InitialValue = req.InitialValue;
        asset.AcquisitionDate = req.AcquisitionDate;
        asset.AnnualDepreciationPercent = asset.Kind == AssetTypes.Depreciable ? req.AnnualDepreciationPercent : null;
        asset.IsArchived = req.IsArchived;
        await db.SaveChangesAsync();

        return await Get(id);
    }

    // Sem aportes: exclui. Com aportes: arquiva (sai do patrimônio, mas o histórico dos aportes continua).
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var asset = await db.Assets.FirstOrDefaultAsync(a => a.Id == id && a.UserId == UserId);
        if (asset == null) return NotFound();

        if (await db.Investments.AnyAsync(i => i.AssetId == id))
        {
            asset.IsArchived = true;
            await db.SaveChangesAsync();
            return Ok(new { message = "Item arquivado, pois possui aportes registrados.", archived = true });
        }

        db.Assets.Remove(asset);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static string TypeError(string kind) => kind switch
    {
        AssetTypes.Investment => $"Tipo inválido. Use: {string.Join(", ", AssetTypes.InvestmentTypes)}.",
        AssetTypes.Depreciable => $"Tipo inválido. Use: {string.Join(", ", AssetTypes.DepreciableTypes)}.",
        _ => "Tipo de patrimônio inválido. Use \"investment\" ou \"depreciable\"."
    };
}
