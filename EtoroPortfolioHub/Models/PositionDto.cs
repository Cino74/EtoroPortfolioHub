namespace EtoroPortfolioHub.Models;

public sealed class PositionDto
{
    public long PositionId { get; set; }
    public int InstrumentId { get; set; }

    public string Symbol { get; set; } = string.Empty;
    public string InstrumentName { get; set; } = string.Empty;

    public bool IsBuy { get; set; }

    public decimal InvestedAmount { get; set; }
    public decimal OpenRate { get; set; }
    public decimal CurrentRate { get; set; }
    public decimal NetProfit { get; set; }

    public decimal Units { get; set; }
    public decimal Leverage { get; set; }
    public decimal TakeProfitRate { get; set; }
    public decimal StopLossRate { get; set; }

    public decimal Bid { get; set; }
    public decimal Ask { get; set; }
    public decimal LastExecution { get; set; }

    public decimal OpenConversionRate { get; set; }
    public decimal ConversionRateBid { get; set; }
    public decimal ConversionRateAsk { get; set; }


    public DateTimeOffset? Timestamp { get; set; }

    public decimal ProfitPercent =>
        InvestedAmount == 0 ? 0 : Math.Round((NetProfit / InvestedAmount) * 100m, 2);
}