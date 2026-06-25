using System.Globalization;
using EtoroPortfolioHub.Data;
using EtoroPortfolioHub.Data.Entities;
using EtoroPortfolioHub.Models;
using EtoroPortfolioHub.State;
using Microsoft.EntityFrameworkCore;

namespace EtoroPortfolioHub.Services;

public sealed class DividendCalendarService
{
    private readonly ApplicationDbContext _db;
    private readonly PortfolioState _portfolioState;
    private readonly CurrentUserService _currentUserService;

    public DividendCalendarService(
        ApplicationDbContext db,
        PortfolioState portfolioState,
        CurrentUserService currentUserService)
    {
        _db = db;
        _portfolioState = portfolioState;
        _currentUserService = currentUserService;
    }

    // =========================
    // CRUD base
    // =========================

    public async Task<List<DividendEventItem>> GetAllAsync()
    {
        var userId = await _currentUserService.GetRequiredUserIdAsync();

        return await _db.DividendEvents
            .Where(x => x.UserId == userId)
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

    public async Task<DividendEventItem?> GetByIdAsync(int id)
    {
        var userId = await _currentUserService.GetRequiredUserIdAsync();

        var entity = await _db.DividendEvents
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);

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

    public async Task SaveOrUpdateAsync(DividendEventItem item)
    {
        var userId = await _currentUserService.GetRequiredUserIdAsync();
        var utcNow = DateTime.UtcNow;

        var normalizedSymbol = NormalizeSymbol(item.Symbol);

        if (string.IsNullOrWhiteSpace(normalizedSymbol))
            throw new InvalidOperationException("Il simbolo è obbligatorio.");

        if (string.IsNullOrWhiteSpace(item.CompanyName))
            throw new InvalidOperationException("Il nome società è obbligatorio.");

        if (!item.PaymentDate.HasValue)
            throw new InvalidOperationException("La Payment Date è obbligatoria.");

        if (item.ExDividendDate.HasValue &&
            item.PaymentDate.HasValue &&
            item.PaymentDate.Value.Date < item.ExDividendDate.Value.Date)
        {
            throw new InvalidOperationException("La Payment Date non può essere precedente alla Ex-Dividend Date.");
        }

        if (item.AnnualDividend < 0)
            throw new InvalidOperationException("Annual Dividend non può essere negativo.");

        if (item.PeriodicDividend < 0)
            throw new InvalidOperationException("Periodic Dividend non può essere negativo.");

        var duplicateExists = await _db.DividendEvents
            .AnyAsync(x =>
                x.UserId == userId &&
                x.Id != item.Id &&
                x.Symbol == normalizedSymbol &&
                x.ExDividendDate == item.ExDividendDate &&
                x.PaymentDate == item.PaymentDate);

        if (duplicateExists)
        {
            throw new InvalidOperationException(
                "Esiste già un evento dividendo con lo stesso simbolo, Ex-Date e Payment Date per questo utente.");
        }

        if (item.Id == 0)
        {
            var entity = new DividendEventEntity
            {
                UserId = userId,
                InstrumentId = item.InstrumentId,
                Symbol = normalizedSymbol,
                CompanyName = item.CompanyName.Trim(),
                Sector = item.Sector?.Trim() ?? string.Empty,
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
                .FirstOrDefaultAsync(x => x.Id == item.Id && x.UserId == userId);

            if (entity is null)
                throw new InvalidOperationException("Evento dividendo non trovato.");

            entity.InstrumentId = item.InstrumentId;
            entity.Symbol = normalizedSymbol;
            entity.CompanyName = item.CompanyName.Trim();
            entity.Sector = item.Sector?.Trim() ?? string.Empty;
            entity.ExDividendDate = item.ExDividendDate;
            entity.PaymentDate = item.PaymentDate;
            entity.AnnualDividend = item.AnnualDividend;
            entity.PeriodicDividend = item.PeriodicDividend;
            entity.Notes = item.Notes?.Trim() ?? string.Empty;
            entity.UpdatedUtc = utcNow;
        }

        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var userId = await _currentUserService.GetRequiredUserIdAsync();

        var entity = await _db.DividendEvents
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);

        if (entity is null)
            return;

        _db.DividendEvents.Remove(entity);
        await _db.SaveChangesAsync();
    }

    public async Task ClearAsync()
    {
        var userId = await _currentUserService.GetRequiredUserIdAsync();

        var entities = await _db.DividendEvents
            .Where(x => x.UserId == userId)
            .ToListAsync();

        if (entities.Count == 0)
            return;

        _db.DividendEvents.RemoveRange(entities);
        await _db.SaveChangesAsync();
    }

    // =========================
    // Calcoli per pagina Dividendi
    // =========================

    public async Task<List<DividendCalendarItemDto>> GetDividendPositionsAsync(
        CancellationToken cancellationToken = default)
    {
        var userId = await _currentUserService.GetRequiredUserIdAsync();

        var snapshot = _portfolioState.GetSnapshot();

        var dividendEvents = await _db.DividendEvents
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.PaymentDate)
            .ThenBy(x => x.Symbol)
            .ToListAsync(cancellationToken);

        if (dividendEvents.Count == 0)
            return new List<DividendCalendarItemDto>();

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

    public async Task<List<DividendMonthlySummaryDto>> GetMonthlySummariesAsync(
        CancellationToken cancellationToken = default)
    {
        var items = await GetDividendPositionsAsync(cancellationToken);

        return items
            .Where(x => x.PaymentDate.HasValue)
            .GroupBy(x => new
            {
                x.PaymentDate!.Value.Year,
                x.PaymentDate!.Value.Month
            })
            .Select(g => new DividendMonthlySummaryDto
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                Label = new DateTime(g.Key.Year, g.Key.Month, 1)
                    .ToString("MMMM yyyy", CultureInfo.GetCultureInfo("it-IT")),
                EstimatedGrossTotal = Math.Round(g.Sum(x => x.EstimatedGrossAmount), 2),
                Items = g
                    .OrderBy(x => x.PaymentDate)
                    .ThenBy(x => x.Symbol)
                    .ToList()
            })
            .OrderBy(x => x.Year)
            .ThenBy(x => x.Month)
            .ToList();
    }

    // =========================
    // Helpers
    // =========================

    private static string NormalizeSymbol(string? symbol)
    {
        return string.IsNullOrWhiteSpace(symbol)
            ? string.Empty
            : symbol.Trim().ToUpperInvariant();
    }
}