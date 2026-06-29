using EtoroPortfolioHub.Data;
using EtoroPortfolioHub.Data.Entities;
using EtoroPortfolioHub.Models;
using Microsoft.EntityFrameworkCore;

namespace EtoroPortfolioHub.Services;

public sealed class DividendCalendarService
{
    private readonly ApplicationDbContext _db;
    private readonly CurrentUserService _currentUserService;

    public DividendCalendarService(
        ApplicationDbContext db,
        CurrentUserService currentUserService)
    {
        _db = db;
        _currentUserService = currentUserService;
    }

    public async Task<List<DividendEventItem>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var userId = await _currentUserService.GetRequiredUserIdAsync();

        return await _db.DividendEvents
            .AsNoTracking()
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
            .ToListAsync(cancellationToken);
    }

    public async Task<DividendEventItem?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var userId = await _currentUserService.GetRequiredUserIdAsync();

        var entity = await _db.DividendEvents
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == id && x.UserId == userId,
                cancellationToken);

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

    public async Task SaveOrUpdateAsync(
        DividendEventItem item,
        CancellationToken cancellationToken = default)
    {
        var userId = await _currentUserService.GetRequiredUserIdAsync();
        var utcNow = DateTime.UtcNow;

        ValidateItem(item);

        var normalizedSymbol = NormalizeSymbol(item.Symbol);

        var duplicateExists = await _db.DividendEvents
            .AnyAsync(
                x =>
                    x.UserId == userId &&
                    x.Id != item.Id &&
                    x.Symbol == normalizedSymbol &&
                    x.ExDividendDate == item.ExDividendDate &&
                    x.PaymentDate == item.PaymentDate,
                cancellationToken);

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
                AnnualDividend = Math.Round(item.AnnualDividend, 6),
                PeriodicDividend = Math.Round(item.PeriodicDividend, 6),
                Notes = item.Notes?.Trim() ?? string.Empty,
                CreatedUtc = utcNow,
                UpdatedUtc = utcNow
            };

            await _db.DividendEvents.AddAsync(entity, cancellationToken);
        }
        else
        {
            var entity = await _db.DividendEvents
                .FirstOrDefaultAsync(
                    x => x.Id == item.Id && x.UserId == userId,
                    cancellationToken);

            if (entity is null)
                throw new InvalidOperationException("Evento dividendo non trovato.");

            entity.InstrumentId = item.InstrumentId;
            entity.Symbol = normalizedSymbol;
            entity.CompanyName = item.CompanyName.Trim();
            entity.Sector = item.Sector?.Trim() ?? string.Empty;
            entity.ExDividendDate = item.ExDividendDate;
            entity.PaymentDate = item.PaymentDate;
            entity.AnnualDividend = Math.Round(item.AnnualDividend, 6);
            entity.PeriodicDividend = Math.Round(item.PeriodicDividend, 6);
            entity.Notes = item.Notes?.Trim() ?? string.Empty;
            entity.UpdatedUtc = utcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var userId = await _currentUserService.GetRequiredUserIdAsync();

        var entity = await _db.DividendEvents
            .FirstOrDefaultAsync(
                x => x.Id == id && x.UserId == userId,
                cancellationToken);

        if (entity is null)
            return;

        _db.DividendEvents.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task ClearAsync(
        CancellationToken cancellationToken = default)
    {
        var userId = await _currentUserService.GetRequiredUserIdAsync();

        var entities = await _db.DividendEvents
            .Where(x => x.UserId == userId)
            .ToListAsync(cancellationToken);

        if (entities.Count == 0)
            return;

        _db.DividendEvents.RemoveRange(entities);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var userId = await _currentUserService.GetRequiredUserIdAsync();

        return await _db.DividendEvents
            .AnyAsync(
                x => x.Id == id && x.UserId == userId,
                cancellationToken);
    }

    private static void ValidateItem(DividendEventItem item)
    {
        if (string.IsNullOrWhiteSpace(item.Symbol))
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
    }

    private static string NormalizeSymbol(string? symbol)
    {
        return string.IsNullOrWhiteSpace(symbol)
            ? string.Empty
            : symbol.Trim().ToUpperInvariant();
    }
}