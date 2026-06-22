namespace EtoroPortfolioHub.Data.Entities;

public sealed class PortfolioTargetEntity
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public int InstrumentId { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string InstrumentName { get; set; } = string.Empty;

    public decimal TargetPercentage { get; set; }

    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}