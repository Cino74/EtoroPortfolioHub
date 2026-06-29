namespace EtoroPortfolioHub.Models;

public sealed class EtoroConnectionItem
{
    public string Environment { get; set; } = "Demo";
    public string PermissionMode { get; set; } = "Read";

    public bool IsConfigured { get; set; }

    public long? Gcid { get; set; }
    public long? RealCid { get; set; }
    public long? DemoCid { get; set; }

    public DateTime? LastSuccessfulValidationUtc { get; set; }
    public string LastValidationMessage { get; set; } = string.Empty;
}