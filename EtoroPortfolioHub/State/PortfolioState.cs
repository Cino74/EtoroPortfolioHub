using EtoroPortfolioHub.Models;

namespace EtoroPortfolioHub.State;

public sealed class PortfolioState
{
    private readonly object _lock = new();

    private PortfolioSnapshot _snapshot = new()
    {
        Positions = new List<PositionDto>(),
        LastUpdated = DateTimeOffset.UtcNow
    };

    public PortfolioSnapshot GetSnapshot()
    {
        lock (_lock)
        {
            return new PortfolioSnapshot
            {
                LastUpdated = _snapshot.LastUpdated,
                Positions = _snapshot.Positions
                    .Select(p => new PositionDto
                    {
                        PositionId = p.PositionId,
                        InstrumentId = p.InstrumentId,
                        Symbol = p.Symbol,
                        InstrumentName = p.InstrumentName,
                        IsBuy = p.IsBuy,
                        InvestedAmount = p.InvestedAmount,
                        OpenRate = p.OpenRate,
                        CurrentRate = p.CurrentRate,
                        NetProfit = p.NetProfit
                    })
                    .ToList()
            };
        }
    }

    public void SetSnapshot(PortfolioSnapshot snapshot)
    {
        lock (_lock)
        {
            _snapshot = snapshot;
        }
    }
}
