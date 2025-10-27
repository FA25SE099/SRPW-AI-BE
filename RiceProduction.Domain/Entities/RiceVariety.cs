namespace RiceProduction.Domain.Entities;

public class RiceVariety : BaseAuditableEntity
{
    [Required]
    [MaxLength(255)]
    public string VarietyName { get; set; } = string.Empty;

    [Required]
    public Guid CategoryId { get; set; }

    public int? BaseGrowthDurationDays { get; set; }

    /// <summary>
    /// Base yield per hectare - actual yield may vary by season (see RiceVarietySeason)
    /// </summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal? BaseYieldPerHectare { get; set; }

    public string? Description { get; set; }

    /// <summary>
    /// General characteristics of this rice variety
    /// </summary>
    public string? Characteristics { get; set; }

    /// <summary>
    /// Whether this variety is currently active/available for planting
    /// </summary>
    public bool IsActive { get; set; } = true;

    [ForeignKey("CategoryId")]
    public RiceVarietyCategory Category { get; set; } = null!;
    
    public ICollection<RiceVarietySeason> RiceVarietySeasons { get; set; } = new List<RiceVarietySeason>();
    public ICollection<Group> Groups { get; set; } = new List<Group>();
    public ICollection<PlotCultivation> PlotCultivations { get; set; } = new List<PlotCultivation>();
}
