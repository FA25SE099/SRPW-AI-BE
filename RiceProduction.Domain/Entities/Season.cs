namespace RiceProduction.Domain.Entities;
//status: IsActive: true false không cần xài
public class Season : BaseAuditableEntity
{
    [Required]
    [MaxLength(100)]
    public string SeasonName { get; set; } = string.Empty;

    [Required]
    public string StartDate { get; set; } = string.Empty; // Format "MM/DD"

    [Required]
    public string EndDate { get; set; } = string.Empty; // Format "MM/DD"

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
    public ICollection<LateFarmerRecord> LateFarmerRecords { get; set; } = new List<LateFarmerRecord>();
}
