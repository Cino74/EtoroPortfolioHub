namespace EtoroPortfolioHub.Models;

public sealed class InstrumentRateDto
{
    public int InstrumentId { get; set; }
    public decimal Ask { get; set; }
    public decimal Bid { get; set; }
    public decimal LastExecution { get; set; }
    public DateTimeOffset? Date { get; set; }
}
