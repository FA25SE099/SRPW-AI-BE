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

    /// <summary>
    /// The rice variety determined by the expert for this entire cluster season
    /// </summary>
    [Required]
    public Guid RiceVarietyId { get; set; }

    /// <summary>
    /// Actual start date for this season instance
    /// </summary>
    [Required]
    public DateTime StartDate { get; set; }
    public int? MaterialConfirmationDaysBeforePlanting { get; set; }
    /// <summary>
    /// Actual end date for this season instance
    /// </summary>
    [Required]
    public DateTime EndDate { get; set; }

    /// <summary>
    /// Break period start (between this and next season)
    /// </summary>
    public DateTime? BreakStartDate { get; set; }
    public int AllowedPlantingFlexibilityDays { get; set; } = 0;

    /// <summary>
    /// Break period end
    /// </summary>
    public DateTime? BreakEndDate { get; set; }

    /// <summary>
    /// When supervisors should start creating production plans
    /// </summary>
    public DateTime? PlanningWindowStart { get; set; }

    /// <summary>
    /// Deadline for production plan creation
    /// </summary>
    public DateTime? PlanningWindowEnd { get; set; }

    /// <summary>
    /// Status of this season instance
    /// </summary>
    public SeasonStatus Status { get; set; } = SeasonStatus.Draft;

    public string? Notes { get; set; }

    [ForeignKey("SeasonId")]
    public Season Season { get; set; } = null!;

    [ForeignKey("ClusterId")]
    public Cluster Cluster { get; set; } = null!;

    [ForeignKey("ManagedByExpertId")]
    public AgronomyExpert? ManagedByExpert { get; set; }

    [ForeignKey("RiceVarietyId")]
    public RiceVariety RiceVariety { get; set; } = null!;
    
    public ICollection<Group> Groups { get; set; } = new List<Group>();
}

