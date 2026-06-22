namespace EtoroPortfolioHub.Models;

public sealed class PortfolioTargetItem
{
    public int InstrumentId { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string InstrumentName { get; set; } = string.Empty;

    /// <summary>
    /// Percentuale target desiderata nel portafoglio (es. 5.50 = 5,50%)
    /// </summary>
    public decimal TargetPercentage { get; set; }
}