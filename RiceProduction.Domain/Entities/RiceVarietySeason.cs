namespace RiceProduction.Domain.Entities;

public class RiceVarietySeason : BaseAuditableEntity
{
    [Required]
    public Guid RiceVarietyId { get; set; }

    [Required]
    public Guid SeasonId { get; set; }

    /// <summary>
    /// Growth duration in days for this variety in this specific season
    /// </summary>
    [Required]
    public int GrowthDurationDays { get; set; }

    /// <summary>
    /// Expected yield per hectare for this variety in this season
    /// </summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal? ExpectedYieldPerHectare { get; set; }

    /// <summary>
    /// Optimal planting window start within the season
    /// </summary>
    public DateTime? OptimalPlantingStart { get; set; }

    /// <summary>
    /// Optimal planting window end within the season
    /// </summary>
    public DateTime? OptimalPlantingEnd { get; set; }

    /// <summary>
    /// Risk level for this variety in this season (Low, Medium, High)
    /// </summary>
    public RiskLevel RiskLevel { get; set; } = RiskLevel.Medium;

    /// <summary>
    /// Special considerations or notes for this variety-season combination
    /// </summary>
    public string? SeasonalNotes { get; set; }

    /// <summary>
    /// Whether this variety is recommended for this season
    /// </summary>
    public bool IsRecommended { get; set; } = true;

    // Navigation properties
    [ForeignKey("RiceVarietyId")]
    public RiceVariety RiceVariety { get; set; } = null!;

    [ForeignKey("SeasonId")]
    public Season Season { get; set; } = null!;
}