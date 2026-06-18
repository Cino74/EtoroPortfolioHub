using System.Text.Json;
using EtoroPortfolioHub.Models;

namespace EtoroPortfolioHub.Services;

public sealed class LegacyPortfolioTargetsMigrationHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<LegacyPortfolioTargetsMigrationHostedService> _logger;

    public LegacyPortfolioTargetsMigrationHostedService(
        IServiceScopeFactory scopeFactory,
        IWebHostEnvironment environment,
        ILogger<LegacyPortfolioTargetsMigrationHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _environment = environment;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var dataFolder = Path.Combine(_environment.ContentRootPath, "App_Data");
            var legacyFilePath = Path.Combine(dataFolder, "portfolio-targets.json");

            if (!File.Exists(legacyFilePath))
            {
                _logger.LogInformation("Migrazione target legacy: file JSON non trovato, nessuna migrazione necessaria.");
                return;
            }

            var json = await File.ReadAllTextAsync(legacyFilePath, cancellationToken);

            if (string.IsNullOrWhiteSpace(json))
            {
                _logger.LogInformation("Migrazione target legacy: file JSON vuoto, nessuna migrazione necessaria.");
                return;
            }

            var legacyItems = JsonSerializer.Deserialize<List<PortfolioTargetItem>>(json)
                              ?? new List<PortfolioTargetItem>();

            if (legacyItems.Count == 0)
            {
                _logger.LogInformation("Migrazione target legacy: nessun target da importare.");
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var portfolioTargetService = scope.ServiceProvider.GetRequiredService<PortfolioTargetService>();

            // Per ora usiamo il default user del nuovo service.
            var existing = await portfolioTargetService.GetAllAsync();

            if (existing.Count > 0)
            {
                _logger.LogInformation(
                    "Migrazione target legacy: il database contiene già {Count} target. Migrazione saltata per evitare duplicati.",
                    existing.Count);
                return;
            }

            await portfolioTargetService.SaveAllAsync(legacyItems);

            var backupFileName = $"portfolio-targets.migrated.{DateTime.UtcNow:yyyyMMddHHmmss}.json";
            var backupFilePath = Path.Combine(dataFolder, backupFileName);

            File.Move(legacyFilePath, backupFilePath);

            _logger.LogInformation(
                "Migrazione target legacy completata con successo. Importati {Count} target. Backup creato: {BackupFile}",
                legacyItems.Count,
                backupFileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore durante la migrazione dei target legacy dal file JSON al database.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}