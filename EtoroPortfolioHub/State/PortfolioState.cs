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
                Credit = _snapshot.Credit,
                UnrealizedPnL = _snapshot.UnrealizedPnL,
                AvailableCash = _snapshot.AvailableCash,
                ProfitLoss = _snapshot.ProfitLoss,

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
                        NetProfit = p.NetProfit,

                        Units = p.Units,
                        Leverage = p.Leverage,
                        TakeProfitRate = p.TakeProfitRate,
                        StopLossRate = p.StopLossRate,

                        Bid = p.Bid,
                        Ask = p.Ask,
                        LastExecution = p.LastExecution,

                        OpenConversionRate = p.OpenConversionRate,
                        ConversionRateBid = p.ConversionRateBid,
                        ConversionRateAsk = p.ConversionRateAsk,

                        InstrumentTypeId = p.InstrumentTypeId,
                        InstrumentTypeDescription = p.InstrumentTypeDescription,

                        Timestamp = p.Timestamp
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