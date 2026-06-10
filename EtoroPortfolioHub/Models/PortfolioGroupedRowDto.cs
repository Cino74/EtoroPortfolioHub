namespace EtoroPortfolioHub.Models;

public sealed class PortfolioGroupedRowDto
{
    public int InstrumentId { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string InstrumentName { get; set; } = string.Empty;

    public string Direction { get; set; } = string.Empty;

    public decimal TotalInvested { get; set; }
    public decimal TotalUnits { get; set; }
    public decimal AverageOpenRate { get; set; }
    public decimal CurrentRate { get; set; }
    public decimal TotalPnL { get; set; }
    public decimal ProfitPercent { get; set; }

    public decimal AverageLeverage { get; set; }
    public decimal AverageTakeProfitRate { get; set; }
    public decimal AverageStopLossRate { get; set; }

    public int PositionsCount { get; set; }
    public DateTimeOffset? LastTimestamp { get; set; }
}