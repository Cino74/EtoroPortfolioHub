namespace EtoroPortfolioHub.Models;

public sealed class PortfolioSnapshot
{
    public List<PositionDto> Positions { get; set; } = new();
    public DateTimeOffset LastUpdated { get; set; } = DateTimeOffset.UtcNow;

    // valori raw / di supporto
    public decimal Credit { get; set; }
    public decimal UnrealizedPnL { get; set; }

    // valori principali da mostrare in UI
    public decimal AvailableCash { get; set; }
    public decimal ProfitLoss { get; set; }

    public int TotalPositions => Positions.Count;
    public decimal TotalInvested => Positions.Sum(p => p.InvestedAmount);
}
