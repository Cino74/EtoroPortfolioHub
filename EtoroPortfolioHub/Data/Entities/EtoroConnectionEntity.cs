namespace EtoroPortfolioHub.Data.Entities;

public sealed class EtoroConnectionEntity
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string Environment { get; set; } = "Demo";
    public string PermissionMode { get; set; } = "Read";

    public string EncryptedUserKey { get; set; } = string.Empty;

    public long? Gcid { get; set; }
    public long? RealCid { get; set; }
    public long? DemoCid { get; set; }

    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }

    public DateTime? LastSuccessfulValidationUtc { get; set; }
    public string LastValidationMessage { get; set; } = string.Empty;
}