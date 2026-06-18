using System.Globalization;
using EtoroPortfolioHub.Data;
using EtoroPortfolioHub.Data.Entities;
using EtoroPortfolioHub.Models;
using EtoroPortfolioHub.State;
using Microsoft.EntityFrameworkCore;

namespace EtoroPortfolioHub.Services;

public sealed class DividendCalendarService
{
    private const string DefaultUserId = "default";

    private readonly ApplicationDbContext _db;
    private readonly PortfolioState _portfolioState;

    public DividendCalendarService(
        ApplicationDbContext db,
        PortfolioState portfolioState)
    {
        _db = db;
        _portfolioState = portfolioState;
    }

    // =========================
    // CRUD base
    // =========================

    public async Task<List<DividendEventItem>> GetAllAsync(string? userId = null)
    {
        var effectiveUserId = NormalizeUserId(userId);

        return await _db.DividendEvents
            .Where(x => x.UserId == effectiveUserId)
            .OrderBy(x => x.PaymentDate)
            .ThenBy(x => x.Symbol)
            .Select(x => new DividendEventItem
            {
                Id = x.Id,
                InstrumentId = x.InstrumentId,
                Symbol = x.Symbol,
                CompanyName = x.CompanyName,
                Sector = x.Sector,
                ExDividendDate = x.ExDividendDate,
                PaymentDate = x.PaymentDate,
                AnnualDividend = x.AnnualDividend,
                PeriodicDividend = x.PeriodicDividend,
                Notes = x.Notes
            })
            .ToListAsync();
    }

    public async Task<DividendEventItem?> GetByIdAsync(int id, string? userId = null)
    {
        var effectiveUserId = NormalizeUserId(userId);

        var entity = await _db.DividendEvents
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == effectiveUserId);

        if (entity is null)
            return null;

        return new DividendEventItem
        {
            Id = entity.Id,
            InstrumentId = entity.InstrumentId,
            Symbol = entity.Symbol,
            CompanyName = entity.CompanyName,
            Sector = entity.Sector,
            ExDividendDate = entity.ExDividendDate,
            PaymentDate = entity.PaymentDate,
            AnnualDividend = entity.AnnualDividend,
            PeriodicDividend = entity.PeriodicDividend,
            Notes = entity.Notes
        };
    }

    public async Task SaveOrUpdateAsync(DividendEventItem item, string? userId = null)
    {
        var effectiveUserId = NormalizeUserId(userId);
        var utcNow = DateTime.UtcNow;

        // Controllo duplicati logici
        var duplicate = await _db.DividendEvents
            .AnyAsync(x =>
                x.UserId == effectiveUserId &&
                x.Id != item.Id &&
                x.Symbol == item.Symbol &&
                x.ExDividendDate == item.ExDividendDate &&
                x.PaymentDate == item.PaymentDate);

        if (duplicate)
        {
            throw new InvalidOperationException(
                "Esiste già un evento dividendo con lo stesso simbolo e le stesse date per questo utente.");
        }

        if (item.Id == 0)
        {
            var entity = new DividendEventEntity
            {
                UserId = effectiveUserId,
                InstrumentId = item.InstrumentId,
                Symbol = item.Symbol.Trim(),
                CompanyName = item.CompanyName.Trim(),
                Sector = item.Sector.Trim(),
                ExDividendDate = item.ExDividendDate,
                PaymentDate = item.PaymentDate,
                AnnualDividend = item.AnnualDividend,
                PeriodicDividend = item.PeriodicDividend,
                Notes = item.Notes?.Trim() ?? string.Empty,
                CreatedUtc = utcNow,
                UpdatedUtc = utcNow
            };

            await _db.DividendEvents.AddAsync(entity);
        }
        else
        {
            var entity = await _db.DividendEvents
                .FirstOrDefaultAsync(x => x.Id == item.Id && x.UserId == effectiveUserId);

            if (entity is null)
            {
                throw new InvalidOperationException("Evento dividendo non trovato.");
            }

            entity.InstrumentId = item.InstrumentId;
            entity.Symbol = item.Symbol.Trim();
            entity.CompanyName = item.CompanyName.Trim();
            entity.Sector = item.Sector.Trim();
            entity.ExDividendDate = item.ExDividendDate;
            entity.PaymentDate = item.PaymentDate;
            entity.AnnualDividend = item.AnnualDividend;
            entity.PeriodicDividend = item.PeriodicDividend;
            entity.Notes = item.Notes?.Trim() ?? string.Empty;
            entity.UpdatedUtc = utcNow;
        }

        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id, string? userId = null)
    {
        var effectiveUserId = NormalizeUserId(userId);

        var entity = await _db.DividendEvents
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == effectiveUserId);

        if (entity is null)
            return;

        _db.DividendEvents.Remove(entity);
        await _db.SaveChangesAsync();
    }

    public async Task ClearAsync(string? userId = null)
    {
        var effectiveUserId = NormalizeUserId(userId);

        var entities = await _db.DividendEvents
            .Where(x => x.UserId == effectiveUserId)
            .ToListAsync();

        if (entities.Count == 0)
            return;

        _db.DividendEvents.RemoveRange(entities);
        await _db.SaveChangesAsync();
    }

    // =========================
    // Logica di calcolo
    // =========================

    public async Task<List<DividendCalendarItemDto>> GetDividendPositionsAsync(string? userId = null, CancellationToken cancellationToken = default)
    {
        var effectiveUserId = NormalizeUserId(userId);
        var snapshot = _portfolioState.GetSnapshot();

        var dividendEvents = await _db.DividendEvents
            .Where(x => x.UserId == effectiveUserId)
            .OrderBy(x => x.PaymentDate)
            .ThenBy(x => x.Symbol)
            .ToListAsync(cancellationToken);

        if (dividendEvents.Count == 0)
            return new List<DividendCalendarItemDto>();

        // Solo posizioni LONG su stock / ETF
        var holdings = snapshot.Positions
            .Where(p => p.IsBuy)
            .Where(p =>
                p.InstrumentTypeDescription.Contains("ETF", StringComparison.OrdinalIgnoreCase) ||
                p.InstrumentTypeDescription.Contains("STOCK", StringComparison.OrdinalIgnoreCase) ||
                p.InstrumentTypeDescription.Contains("STOCKS", StringComparison.OrdinalIgnoreCase))
            .GroupBy(p => NormalizeSymbol(p.Symbol))
            .Select(g => new
            {
                Symbol = g.Key,
                InstrumentName = g.Select(x => x.InstrumentName).FirstOrDefault() ?? g.Key,
                InstrumentId = g.Select(x => x.InstrumentId).FirstOrDefault(),
                Units = g.Sum(x => x.Units)
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.Symbol) && x.Units > 0)
            .ToList();

        var result = new List<DividendCalendarItemDto>();

        foreach (var holding in holdings)
        {
            var matches = dividendEvents
                .Where(x => NormalizeSymbol(x.Symbol) == holding.Symbol)
                .OrderBy(x => x.PaymentDate ?? DateTime.MaxValue)
                .ToList();

            foreach (var match in matches)
            {
                result.Add(new DividendCalendarItemDto
                {
                    Id = match.Id,
                    InstrumentId = match.InstrumentId ?? holding.InstrumentId,
                    Symbol = match.Symbol,
                    CompanyName = match.CompanyName,
                    InstrumentName = holding.InstrumentName,
                    Sector = match.Sector,
                    ExDividendDate = match.ExDividendDate,
                    PaymentDate = match.PaymentDate,
                    AnnualDividend = match.AnnualDividend,
                    PeriodicDividend = match.PeriodicDividend,
                    UnitsHeld = holding.Units,
                    Notes = match.Notes
                });
            }
        }

        return result
            .OrderBy(x => x.PaymentDate ?? DateTime.MaxValue)
            .ThenBy(x => x.Symbol)
            .ToList();
    }

    public async Task<List<DividendMonthlySummaryDto>> GetMonthlySummariesAsync(string? userId = null, CancellationToken cancellationToken = default)
    {
        var items = await GetDividendPositionsAsync(userId, cancellationToken);

        return items
            .Where(x => x.PaymentDate.HasValue)
            .GroupBy(x => new { x.PaymentDate!.Value.Year, x.PaymentDate!.Value.Month })
            .Select(g => new DividendMonthlySummaryDto
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                Label = new DateTime(g.Key.Year, g.Key.Month, 1)
                    .ToString("MMMM yyyy", CultureInfo.GetCultureInfo("it-IT")),
                EstimatedGrossTotal = Math.Round(g.Sum(x => x.EstimatedGrossAmount), 2),
                Items = g.OrderBy(x => x.PaymentDate).ThenBy(x => x.Symbol).ToList()
            })
            .OrderBy(x => x.Year)
            .ThenBy(x => x.Month)
            .ToList();
    }

    // =========================
    // Helpers
    // =========================

    private static string NormalizeUserId(string? userId)
    {
        return string.IsNullOrWhiteSpace(userId)
            ? DefaultUserId
            : userId.Trim();
    }

    private static string NormalizeSymbol(string symbol)
    {
        return symbol.Trim().ToUpperInvariant();
    }
}