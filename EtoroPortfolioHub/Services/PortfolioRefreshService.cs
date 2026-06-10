using EtoroPortfolioHub.Models;
using EtoroPortfolioHub.State;
using Microsoft.Extensions.Options;

namespace EtoroPortfolioHub.Services;

public sealed class PortfolioRefreshService : BackgroundService
{
    private readonly ILogger<PortfolioRefreshService> _logger;
    private readonly EtoroRestClient _restClient;
    private readonly PortfolioState _portfolioState;
    private readonly EtoroOptions _options;

    public PortfolioRefreshService(
        ILogger<PortfolioRefreshService> logger,
        EtoroRestClient restClient,
        PortfolioState portfolioState,
        IOptions<EtoroOptions> options)
    {
        _logger = logger;
        _restClient = restClient;
        _portfolioState = portfolioState;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PortfolioRefreshService avviato.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var snapshot = await _restClient.GetPortfolioSnapshotAsync(stoppingToken);
                _portfolioState.SetSnapshot(snapshot);

                _logger.LogInformation(
                    "Portfolio aggiornato. Posizioni ricevute: {Count}",
                    snapshot.Positions.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante il refresh del portfolio.");
            }

            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(Math.Max(1, _options.RefreshIntervalSeconds)),
                    stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("PortfolioRefreshService terminato.");
    }
}