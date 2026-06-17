namespace EtoroPortfolioHub.Models;

public sealed class LiveRateUpdateDto
{
    public int InstrumentId { get; set; }

    public decimal Bid { get; set; }
    public decimal Ask { get; set; }
    public decimal LastExecution { get; set; }

    public decimal ConversionRateBid { get; set; }
    public decimal ConversionRateAsk { get; set; }

    public DateTimeOffset? Date { get; set; }
}