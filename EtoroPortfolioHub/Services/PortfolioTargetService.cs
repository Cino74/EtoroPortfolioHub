using System.Text.Json;
using EtoroPortfolioHub.Models;

namespace EtoroPortfolioHub.Services;

public sealed class PortfolioTargetService
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public PortfolioTargetService(IWebHostEnvironment environment)
    {
        var dataFolder = Path.Combine(environment.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dataFolder);

        _filePath = Path.Combine(dataFolder, "portfolio-targets.json");
    }

    public async Task<Dictionary<int, PortfolioTargetItem>> GetAllAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (!File.Exists(_filePath))
                return new Dictionary<int, PortfolioTargetItem>();

            var json = await File.ReadAllTextAsync(_filePath);

            if (string.IsNullOrWhiteSpace(json))
                return new Dictionary<int, PortfolioTargetItem>();

            var items = JsonSerializer.Deserialize<List<PortfolioTargetItem>>(json)
                        ?? new List<PortfolioTargetItem>();

            return items.ToDictionary(x => x.InstrumentId, x => x);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveAllAsync(IEnumerable<PortfolioTargetItem> items)
    {
        await _lock.WaitAsync();
        try
        {
            var list = items
                .OrderBy(x => x.Symbol)
                .ToList();

            var json = JsonSerializer.Serialize(list, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(_filePath, json);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<PortfolioTargetItem?> GetByInstrumentIdAsync(int instrumentId)
    {
        var all = await GetAllAsync();
        return all.TryGetValue(instrumentId, out var item) ? item : null;
    }

    public async Task SaveOrUpdateAsync(PortfolioTargetItem item)
    {
        await _lock.WaitAsync();
        try
        {
            List<PortfolioTargetItem> items;

            if (File.Exists(_filePath))
            {
                var json = await File.ReadAllTextAsync(_filePath);
                items = JsonSerializer.Deserialize<List<PortfolioTargetItem>>(json)
                        ?? new List<PortfolioTargetItem>();
            }
            else
            {
                items = new List<PortfolioTargetItem>();
            }

            var existing = items.FirstOrDefault(x => x.InstrumentId == item.InstrumentId);

            if (existing is null)
            {
                items.Add(item);
            }
            else
            {
                existing.Symbol = item.Symbol;
                existing.InstrumentName = item.InstrumentName;
                existing.TargetPercentage = item.TargetPercentage;
            }

            var updatedJson = JsonSerializer.Serialize(
                items.OrderBy(x => x.Symbol).ToList(),
                new JsonSerializerOptions
                {
                    WriteIndented = true
                });

            await File.WriteAllTextAsync(_filePath, updatedJson);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task DeleteAsync(int instrumentId)
    {
        await _lock.WaitAsync();
        try
        {
            if (!File.Exists(_filePath))
                return;

            var json = await File.ReadAllTextAsync(_filePath);

            var items = JsonSerializer.Deserialize<List<PortfolioTargetItem>>(json)
                        ?? new List<PortfolioTargetItem>();

            items = items
                .Where(x => x.InstrumentId != instrumentId)
                .OrderBy(x => x.Symbol)
                .ToList();

            var updatedJson = JsonSerializer.Serialize(items, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(_filePath, updatedJson);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task ClearAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (File.Exists(_filePath))
            {
                await File.WriteAllTextAsync(_filePath, "[]");
            }
        }
        finally
        {
            _lock.Release();
        }
    }
}