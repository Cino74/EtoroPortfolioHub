using EtoroPortfolioHub.Data;
using EtoroPortfolioHub.Models;
using EtoroPortfolioHub.State;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EtoroPortfolioHub.Services;

public sealed class PortfolioRefreshService : BackgroundService
{
    private const string ProtectorPurpose = "EtoroPortfolioHub.EtoroUserKey.v1";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly PortfolioState _portfolioState;
    private readonly IDataProtector _protector;
    private readonly IOptionsMonitor<EtoroOptions> _optionsMonitor;
    private readonly ILogger<PortfolioRefreshService> _logger;

    public PortfolioRefreshService(
        IServiceScopeFactory scopeFactory,
        PortfolioState portfolioState,
        IDataProtectionProvider dataProtectionProvider,
        IOptionsMonitor<EtoroOptions> optionsMonitor,
        ILogger<PortfolioRefreshService> logger)
    {
        _scopeFactory = scopeFactory;
        _portfolioState = portfolioState;
        _protector = dataProtectionProvider.CreateProtector(ProtectorPurpose);
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var refreshIntervalSeconds = Math.Max(
            10,
            _optionsMonitor.CurrentValue.RefreshIntervalSeconds);

        _logger.LogInformation(
            "PortfolioRefreshService avviato. Refresh ogni {Seconds} secondi.",
            refreshIntervalSeconds);

        await RefreshAllConfiguredUsersAsync(stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(refreshIntervalSeconds));

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RefreshAllConfiguredUsersAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("PortfolioRefreshService arrestato.");
        }
    }

    private async Task RefreshAllConfiguredUsersAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var etoroRestClient = scope.ServiceProvider.GetRequiredService<EtoroRestClient>();

        var connections = await db.EtoroConnections
            .Where(x => !string.IsNullOrWhiteSpace(x.EncryptedUserKey))
            .OrderBy(x => x.UserId)
            .ToListAsync(cancellationToken);

        if (connections.Count == 0)
        {
            _logger.LogDebug("Nessuna connessione eToro configurata. Refresh saltato.");
            return;
        }

        _logger.LogInformation(
            "Refresh portafogli eToro per {Count} utenti configurati.",
            connections.Count);

        foreach (var connection in connections)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                var userKey = _protector.Unprotect(connection.EncryptedUserKey);
                var environment = NormalizeEnvironment(connection.Environment);

                _logger.LogInformation(
                    "Refresh portafoglio eToro per UserId {UserId}, ambiente {Environment}.",
                    connection.UserId,
                    environment);

                var snapshot = await etoroRestClient.GetPortfolioSnapshotAsync(
                    userKey,
                    environment,
                    cancellationToken);

                _portfolioState.SetSnapshot(connection.UserId, snapshot);

                connection.LastSuccessfulValidationUtc = DateTime.UtcNow;
                connection.LastValidationMessage = "Refresh portafoglio completato correttamente.";
                connection.UpdatedUtc = DateTime.UtcNow;

                await db.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "Refresh portafoglio completato per UserId {UserId}. Posizioni: {PositionsCount}.",
                    connection.UserId,
                    snapshot.Positions.Count);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                connection.LastValidationMessage = $"Errore refresh portafoglio: {ex.Message}";
                connection.UpdatedUtc = DateTime.UtcNow;

                await db.SaveChangesAsync(cancellationToken);

                _logger.LogWarning(
                    ex,
                    "Errore durante il refresh portafoglio per UserId {UserId}.",
                    connection.UserId);
            }
        }
    }

    private static string NormalizeEnvironment(string? environment)
    {
        return string.Equals(environment, "Real", StringComparison.OrdinalIgnoreCase)
            ? "Real"
            : "Demo";
    }
}