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

    public event Action? Changed;

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

        Changed?.Invoke();
    }

    public List<int> GetInstrumentIds()
    {
        lock (_lock)
        {
            return _snapshot.Positions
                .Select(p => p.InstrumentId)
                .Where(id => id > 0)
                .Distinct()
                .ToList();
        }
    }

    public void ApplyLiveRate(LiveRateUpdateDto rate)
    {
        lock (_lock)
        {
            foreach (var position in _snapshot.Positions.Where(p => p.InstrumentId == rate.InstrumentId))
            {
                position.Bid = rate.Bid;
                position.Ask = rate.Ask;
                position.LastExecution = rate.LastExecution;
                position.ConversionRateBid = rate.ConversionRateBid;
                position.ConversionRateAsk = rate.ConversionRateAsk;

                // aggiorniamo solo il prezzo attuale mostrato in UI
                position.CurrentRate = rate.LastExecution != 0m
                    ? rate.LastExecution
                    : (rate.Bid != 0m ? rate.Bid : rate.Ask);

                if (rate.Date is not null)
                {
                    position.Timestamp = rate.Date;
                }
            }

            // NON ricalcoliamo qui NetProfit / ProfitLoss / UnrealizedPnL
            // per evitare valori incoerenti rispetto a eToro.
            _snapshot.LastUpdated = DateTimeOffset.UtcNow;
        }

        Changed?.Invoke();
    }
}