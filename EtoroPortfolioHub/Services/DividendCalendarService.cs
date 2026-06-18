using System.Globalization;
using System.Text.Json;
using EtoroPortfolioHub.Models;
using EtoroPortfolioHub.State;

namespace EtoroPortfolioHub.Services;

public sealed class DividendCalendarService
{
    private readonly string _filePath;
    private readonly PortfolioState _portfolioState;
    private readonly ILogger<DividendCalendarService> _logger;

    public DividendCalendarService(
        IWebHostEnvironment environment,
        PortfolioState portfolioState,
        ILogger<DividendCalendarService> logger)
    {
        _portfolioState = portfolioState;
        _logger = logger;

        var dataFolder = Path.Combine(environment.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dataFolder);

        _filePath = Path.Combine(dataFolder, "dividend-calendar.json");
    }

    public async Task<List<DividendCalendarItemDto>> GetDividendPositionsAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = _portfolioState.GetSnapshot();
        var calendarItems = await LoadCalendarFromFileAsync(cancellationToken);

        if (calendarItems.Count == 0)
            return new List<DividendCalendarItemDto>();

        // Solo posizioni LONG su Stock/ETF
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

        var matched = new List<DividendCalendarItemDto>();

        foreach (var holding in holdings)
        {
            var exactMatch = calendarItems
                .Where(x => NormalizeSymbol(x.Symbol) == holding.Symbol)
                .OrderBy(x => x.PaymentDate ?? DateTime.MaxValue)
                .FirstOrDefault();

            if (exactMatch is null)
                continue;

            matched.Add(new DividendCalendarItemDto
            {
                Symbol = exactMatch.Symbol,
                CompanyName = exactMatch.CompanyName,
                Sector = exactMatch.Sector,
                ExDividendDate = exactMatch.ExDividendDate,
                PaymentDate = exactMatch.PaymentDate,
                AnnualDividend = exactMatch.AnnualDividend,
                PeriodicDividend = exactMatch.PeriodicDividend,
                UnitsHeld = holding.Units,
                InstrumentName = holding.InstrumentName,
                InstrumentId = holding.InstrumentId
            });
        }

        return matched
            .OrderBy(x => x.PaymentDate ?? DateTime.MaxValue)
            .ThenBy(x => x.Symbol)
            .ToList();
    }

    public async Task<List<DividendMonthlySummaryDto>> GetMonthlySummariesAsync(CancellationToken cancellationToken = default)
    {
        var items = await GetDividendPositionsAsync(cancellationToken);

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

    private async Task<List<DividendCalendarItemDto>> LoadCalendarFromFileAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            _logger.LogWarning("File dividend-calendar.json non trovato in {Path}", _filePath);
            return new List<DividendCalendarItemDto>();
        }

        var json = await File.ReadAllTextAsync(_filePath, cancellationToken);

        if (string.IsNullOrWhiteSpace(json))
            return new List<DividendCalendarItemDto>();

        var items = JsonSerializer.Deserialize<List<DividendCalendarItemDto>>(json)
                    ?? new List<DividendCalendarItemDto>();

        return items
            .Where(x => !string.IsNullOrWhiteSpace(x.Symbol))
            .ToList();
    }

    private static string NormalizeSymbol(string symbol)
    {
        return symbol
            .Trim()
            .ToUpperInvariant();
    }
}