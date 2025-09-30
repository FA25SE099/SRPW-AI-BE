namespace RiceProduction.Domain.Entities;

public class Season : BaseAuditableEntity
{
    [Required]
    [MaxLength(100)]
    public string SeasonName { get; set; } = string.Empty;

    [Required]
    public DateTime StartDate { get; set; }

    [Required]
    public DateTime EndDate { get; set; }

    /// <summary>
    /// Type of season (e.g., "Wet Season", "Dry Season", "Winter-Spring")
    /// </summary>
    [MaxLength(50)]
    public string? SeasonType { get; set; }

    /// <summary>
    /// Whether this season is currently active for planning
    /// </summary>
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public ICollection<RiceVarietySeason> RiceVarietySeasons { get; set; } = new List<RiceVarietySeason>();
    public ICollection<PlotCultivation> PlotCultivations { get; set; } = new List<PlotCultivation>();
}
