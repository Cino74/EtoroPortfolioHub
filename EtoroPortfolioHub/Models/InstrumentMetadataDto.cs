namespace EtoroPortfolioHub.Models;

public sealed class InstrumentMetadataDto
{
    public int InstrumentId { get; set; }
    public string InstrumentDisplayName { get; set; } = string.Empty;
    public string SymbolFull { get; set; } = string.Empty;

    public int InstrumentTypeId { get; set; }
    public string InstrumentTypeDescription { get; set; } = string.Empty;
}