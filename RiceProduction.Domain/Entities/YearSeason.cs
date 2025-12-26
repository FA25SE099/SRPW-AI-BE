namespace RiceProduction.Domain.Entities;

/// <summary>
/// Represents a specific season instance for a year in a cluster, managed by an expert.
/// The expert determines ONE rice variety that will be grown cluster-wide for this season.
/// </summary>
public class YearSeason : BaseAuditableEntity
{
    [Required]
    public Guid SeasonId { get; set; }

    [Required]
    public Guid ClusterId { get; set; }

    [Required]
    public int Year { get; set; }

    public Guid? ManagedByExpertId { get; set; }

    public Guid? RiceVarietyId { get; set; }
    
    public bool AllowFarmerSelection { get; set; } = false;
    public DateTime? FarmerSelectionWindowStart { get; set; }
    public DateTime? FarmerSelectionWindowEnd { get; set; }

    [Required]
    public DateTime StartDate { get; set; }
    public int? MaterialConfirmationDaysBeforePlanting { get; set; }
    
    [Required]
    public DateTime EndDate { get; set; }

    public DateTime? BreakStartDate { get; set; }
    public int AllowedPlantingFlexibilityDays { get; set; } = 0;

    public DateTime? BreakEndDate { get; set; }

    public DateTime? PlanningWindowStart { get; set; }

    public DateTime? PlanningWindowEnd { get; set; }

    public SeasonStatus Status { get; set; } = SeasonStatus.Draft;

    public string? Notes { get; set; }

    [ForeignKey("SeasonId")]
    public Season Season { get; set; } = null!;

    [ForeignKey("ClusterId")]
    public Cluster Cluster { get; set; } = null!;

    [ForeignKey("ManagedByExpertId")]
    public AgronomyExpert? ManagedByExpert { get; set; }

    [ForeignKey("RiceVarietyId")]
    public RiceVariety? RiceVariety { get; set; }
    
    public ICollection<Group> Groups { get; set; } = new List<Group>();
}

