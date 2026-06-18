namespace EtoroPortfolioHub.Models;

public sealed class DividendEventItem
{
    public int Id { get; set; }

    public int? InstrumentId { get; set; }

    public string Symbol { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string Sector { get; set; } = string.Empty;

    public DateTime? ExDividendDate { get; set; }
    public DateTime? PaymentDate { get; set; }

    public decimal AnnualDividend { get; set; }
    public decimal PeriodicDividend { get; set; }

    public string Notes { get; set; } = string.Empty;
}
