namespace EtoroPortfolioHub.Models;

public sealed class DividendCalendarItemDto
{
    public string Symbol { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string Sector { get; set; } = string.Empty;

    public DateTime? ExDividendDate { get; set; }
    public DateTime? PaymentDate { get; set; }

    public decimal AnnualDividend { get; set; }
    public decimal PeriodicDividend { get; set; }

    public decimal UnitsHeld { get; set; }

    public string InstrumentName { get; set; } = string.Empty;
    public int InstrumentId { get; set; }

    public decimal EstimatedGrossAmount =>
        Math.Round(UnitsHeld * PeriodicDividend, 2);

    public string FrequencyLabel
    {
        get
        {
            if (AnnualDividend <= 0 || PeriodicDividend <= 0)
                return "N/D";

            var ratio = AnnualDividend / PeriodicDividend;

            if (Math.Abs(ratio - 12m) < 0.5m) return "Mensile";
            if (Math.Abs(ratio - 4m) < 0.5m) return "Trimestrale";
            if (Math.Abs(ratio - 2m) < 0.5m) return "Semestrale";
            if (Math.Abs(ratio - 1m) < 0.5m) return "Annuale";

            return "Variabile";
        }
    }
}