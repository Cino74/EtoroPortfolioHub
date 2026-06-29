using EtoroPortfolioHub.Models;
using EtoroPortfolioHub.State;

namespace EtoroPortfolioHub.Services;

public sealed class UserPortfolioService
{
    private readonly CurrentUserService _currentUserService;
    private readonly EtoroCredentialService _etoroCredentialService;
    private readonly EtoroRestClient _etoroRestClient;
    private readonly PortfolioState _portfolioState;
    private readonly ILogger<UserPortfolioService> _logger;

    public UserPortfolioService(
        CurrentUserService currentUserService,
        EtoroCredentialService etoroCredentialService,
        EtoroRestClient etoroRestClient,
        PortfolioState portfolioState,
        ILogger<UserPortfolioService> logger)
    {
        _currentUserService = currentUserService;
        _etoroCredentialService = etoroCredentialService;
        _etoroRestClient = etoroRestClient;
        _portfolioState = portfolioState;
        _logger = logger;
    }

    public async Task<PortfolioSnapshot> GetCurrentUserSnapshotAsync(
        CancellationToken cancellationToken = default)
    {
        var userId = await _currentUserService.GetRequiredUserIdAsync();

        var snapshot = _portfolioState.GetSnapshot(userId);

        if (snapshot.Positions.Count > 0)
        {
            return snapshot;
        }

        return await RefreshCurrentUserSnapshotAsync(cancellationToken);
    }

    public async Task<PortfolioSnapshot> RefreshCurrentUserSnapshotAsync(
        CancellationToken cancellationToken = default)
    {
        var userId = await _currentUserService.GetRequiredUserIdAsync();

        var userKey = await _etoroCredentialService.GetRequiredUserKeyAsync();
        var environment = await _etoroCredentialService.GetRequiredEnvironmentAsync();

        _logger.LogInformation(
            "Refresh portafoglio eToro per utente {UserId}, ambiente {Environment}",
            userId,
            environment);

        var snapshot = await _etoroRestClient.GetPortfolioSnapshotAsync(
            userKey,
            environment,
            cancellationToken);

        _portfolioState.SetSnapshot(userId, snapshot);

        return snapshot;
    }

    public async Task<bool> HasCurrentUserSnapshotAsync()
    {
        var userId = await _currentUserService.GetRequiredUserIdAsync();

        return _portfolioState.HasSnapshot(userId);
    }

    public async Task ClearCurrentUserSnapshotAsync()
    {
        var userId = await _currentUserService.GetRequiredUserIdAsync();

        _portfolioState.ClearSnapshot(userId);
    }

    public async Task<PortfolioSnapshot> GetCachedCurrentUserSnapshotAsync()
    {
        var userId = await _currentUserService.GetRequiredUserIdAsync();

        return _portfolioState.GetSnapshot(userId);
    }
}