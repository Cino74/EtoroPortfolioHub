using EtoroPortfolioHub.Data;
using EtoroPortfolioHub.Data.Entities;
using EtoroPortfolioHub.Models;
using Microsoft.EntityFrameworkCore;

namespace EtoroPortfolioHub.Services;

public sealed class PortfolioTargetService
{
    private const string DefaultUserId = "default";

    private readonly ApplicationDbContext _db;

    public PortfolioTargetService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Dictionary<int, PortfolioTargetItem>> GetAllAsync(string? userId = null)
    {
        var effectiveUserId = NormalizeUserId(userId);

        return await _db.PortfolioTargets
            .Where(x => x.UserId == effectiveUserId)
            .OrderBy(x => x.Symbol)
            .ToDictionaryAsync(
                x => x.InstrumentId,
                x => new PortfolioTargetItem
                {
                    InstrumentId = x.InstrumentId,
                    Symbol = x.Symbol,
                    InstrumentName = x.InstrumentName,
                    TargetPercentage = x.TargetPercentage
                });
    }

    public async Task SaveAllAsync(IEnumerable<PortfolioTargetItem> items, string? userId = null)
    {
        var effectiveUserId = NormalizeUserId(userId);

        var existing = await _db.PortfolioTargets
            .Where(x => x.UserId == effectiveUserId)
            .ToListAsync();

        _db.PortfolioTargets.RemoveRange(existing);

        var utcNow = DateTime.UtcNow;

        var entities = items.Select(x => new PortfolioTargetEntity
        {
            UserId = effectiveUserId,
            InstrumentId = x.InstrumentId,
            Symbol = x.Symbol,
            InstrumentName = x.InstrumentName,
            TargetPercentage = x.TargetPercentage,
            CreatedUtc = utcNow,
            UpdatedUtc = utcNow
        });

        await _db.PortfolioTargets.AddRangeAsync(entities);
        await _db.SaveChangesAsync();
    }

    public async Task<PortfolioTargetItem?> GetByInstrumentIdAsync(int instrumentId, string? userId = null)
    {
        var effectiveUserId = NormalizeUserId(userId);

        var entity = await _db.PortfolioTargets
            .FirstOrDefaultAsync(x =>
                x.UserId == effectiveUserId &&
                x.InstrumentId == instrumentId);

        if (entity is null)
            return null;

        return new PortfolioTargetItem
        {
            InstrumentId = entity.InstrumentId,
            Symbol = entity.Symbol,
            InstrumentName = entity.InstrumentName,
            TargetPercentage = entity.TargetPercentage
        };
    }

    public async Task SaveOrUpdateAsync(PortfolioTargetItem item, string? userId = null)
    {
        var effectiveUserId = NormalizeUserId(userId);
        var utcNow = DateTime.UtcNow;

        var existing = await _db.PortfolioTargets
            .FirstOrDefaultAsync(x =>
                x.UserId == effectiveUserId &&
                x.InstrumentId == item.InstrumentId);

        if (existing is null)
        {
            var entity = new PortfolioTargetEntity
            {
                UserId = effectiveUserId,
                InstrumentId = item.InstrumentId,
                Symbol = item.Symbol,
                InstrumentName = item.InstrumentName,
                TargetPercentage = item.TargetPercentage,
                CreatedUtc = utcNow,
                UpdatedUtc = utcNow
            };

            await _db.PortfolioTargets.AddAsync(entity);
        }
        else
        {
            existing.Symbol = item.Symbol;
            existing.InstrumentName = item.InstrumentName;
            existing.TargetPercentage = item.TargetPercentage;
            existing.UpdatedUtc = utcNow;
        }

        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int instrumentId, string? userId = null)
    {
        var effectiveUserId = NormalizeUserId(userId);

        var existing = await _db.PortfolioTargets
            .FirstOrDefaultAsync(x =>
                x.UserId == effectiveUserId &&
                x.InstrumentId == instrumentId);

        if (existing is null)
            return;

        _db.PortfolioTargets.Remove(existing);
        await _db.SaveChangesAsync();
    }

    public async Task ClearAsync(string? userId = null)
    {
        var effectiveUserId = NormalizeUserId(userId);

        var existing = await _db.PortfolioTargets
            .Where(x => x.UserId == effectiveUserId)
            .ToListAsync();

        if (existing.Count == 0)
            return;

        _db.PortfolioTargets.RemoveRange(existing);
        await _db.SaveChangesAsync();
    }

    private static string NormalizeUserId(string? userId)
    {
        return string.IsNullOrWhiteSpace(userId)
            ? DefaultUserId
            : userId.Trim();
    }
}