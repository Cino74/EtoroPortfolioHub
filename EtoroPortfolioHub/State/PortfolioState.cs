using EtoroPortfolioHub.Models;

namespace EtoroPortfolioHub.State;

public sealed class PortfolioState
{
    private const string LegacyDefaultUserId = "default";

    private readonly object _lock = new();

    private readonly Dictionary<string, PortfolioSnapshot> _snapshotsByUserId = new();

    public PortfolioSnapshot GetSnapshot(string userId)
    {
        var effectiveUserId = NormalizeUserId(userId);

        lock (_lock)
        {
            if (_snapshotsByUserId.TryGetValue(effectiveUserId, out var snapshot))
            {
                return CloneSnapshot(snapshot);
            }

            return CreateEmptySnapshot();
        }
    }

    public void SetSnapshot(string userId, PortfolioSnapshot snapshot)
    {
        var effectiveUserId = NormalizeUserId(userId);

        lock (_lock)
        {
            _snapshotsByUserId[effectiveUserId] = CloneSnapshot(snapshot);
        }
    }

    public bool HasSnapshot(string userId)
    {
        var effectiveUserId = NormalizeUserId(userId);

        lock (_lock)
        {
            return _snapshotsByUserId.ContainsKey(effectiveUserId);
        }
    }

    public void ClearSnapshot(string userId)
    {
        var effectiveUserId = NormalizeUserId(userId);

        lock (_lock)
        {
            _snapshotsByUserId.Remove(effectiveUserId);
        }
    }

    public void ClearAll()
    {
        lock (_lock)
        {
            _snapshotsByUserId.Clear();
        }
    }

    public List<string> GetKnownUserIds()
    {
        lock (_lock)
        {
            return _snapshotsByUserId.Keys.ToList();
        }
    }

    // ---------------------------------------------------------------------
    // Compatibilità temporanea con il codice esistente.
    // Da rimuovere quando Home, Portfolio, PortfolioTargets, Dividends
    // e PortfolioRefreshService saranno tutti aggiornati a usare lo UserId.
    // ---------------------------------------------------------------------

    public PortfolioSnapshot GetSnapshot()
    {
        return GetSnapshot(LegacyDefaultUserId);
    }

    public void SetSnapshot(PortfolioSnapshot snapshot)
    {
        SetSnapshot(LegacyDefaultUserId, snapshot);
    }

    private static string NormalizeUserId(string? userId)
    {
        return string.IsNullOrWhiteSpace(userId)
            ? LegacyDefaultUserId
            : userId.Trim();
    }

    private static PortfolioSnapshot CreateEmptySnapshot()
    {
        return new PortfolioSnapshot
        {
            LastUpdated = DateTimeOffset.UtcNow,
            Positions = new List<PositionDto>()
        };
    }

    private static PortfolioSnapshot CloneSnapshot(PortfolioSnapshot snapshot)
    {
        return new PortfolioSnapshot
        {
            LastUpdated = snapshot.LastUpdated,
            Credit = snapshot.Credit,
            UnrealizedPnL = snapshot.UnrealizedPnL,
            AvailableCash = snapshot.AvailableCash,
            ProfitLoss = snapshot.ProfitLoss,

            Positions = snapshot.Positions
                .Select(ClonePosition)
                .ToList()
        };
    }

    private static PositionDto ClonePosition(PositionDto p)
    {
        return new PositionDto
        {
            PositionId = p.PositionId,
            InstrumentId = p.InstrumentId,

            Symbol = p.Symbol,
            InstrumentName = p.InstrumentName,

            InstrumentTypeId = p.InstrumentTypeId,
            InstrumentTypeDescription = p.InstrumentTypeDescription,

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
        };
    }
}