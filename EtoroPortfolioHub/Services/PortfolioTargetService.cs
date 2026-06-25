using EtoroPortfolioHub.Data;
using EtoroPortfolioHub.Data.Entities;
using EtoroPortfolioHub.Models;
using Microsoft.EntityFrameworkCore;

namespace EtoroPortfolioHub.Services;

public sealed class PortfolioTargetService
{
    private readonly ApplicationDbContext _db;
    private readonly CurrentUserService _currentUserService;

    public PortfolioTargetService(
        ApplicationDbContext db,
        CurrentUserService currentUserService)
    {
        _db = db;
        _currentUserService = currentUserService;
    }

    public async Task<Dictionary<int, PortfolioTargetItem>> GetAllAsync()
    {
        var userId = await _currentUserService.GetRequiredUserIdAsync();

        return await _db.PortfolioTargets
            .Where(x => x.UserId == userId)
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

    public async Task SaveAllAsync(IEnumerable<PortfolioTargetItem> items)
    {
        var userId = await _currentUserService.GetRequiredUserIdAsync();

        var existing = await _db.PortfolioTargets
            .Where(x => x.UserId == userId)
            .ToListAsync();

        _db.PortfolioTargets.RemoveRange(existing);

        var utcNow = DateTime.UtcNow;

        var entities = items.Select(x => new PortfolioTargetEntity
        {
            UserId = userId,
            InstrumentId = x.InstrumentId,
            Symbol = x.Symbol,
            InstrumentName = x.InstrumentName,
            TargetPercentage = Math.Round(x.TargetPercentage, 2),
            CreatedUtc = utcNow,
            UpdatedUtc = utcNow
        });

        await _db.PortfolioTargets.AddRangeAsync(entities);
        await _db.SaveChangesAsync();
    }
}