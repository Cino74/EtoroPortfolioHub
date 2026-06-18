namespace EtoroPortfolioHub.Models;

public sealed class DividendMonthlySummaryDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string Label { get; set; } = string.Empty;

    public decimal EstimatedGrossTotal { get; set; }

    public List<DividendCalendarItemDto> Items { get; set; } = new();
}
