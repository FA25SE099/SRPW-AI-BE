namespace RiceProduction.Domain.Entities;

public class Group : BaseAuditableEntity
{
    [Required]
    public Guid ClusterId { get; set; }

    public Guid? SupervisorId { get; set; }

    public Guid? RiceVarietyId { get; set; }

    public Guid? SeasonId { get; set; }

    /// <summary>
    /// Year of the season cycle (e.g., 2024, 2025)
    /// Required to distinguish between recurring seasons across years
    /// </summary>
    [Required]
    public int Year { get; set; } 

    public DateTime? PlantingDate { get; set; }

    public GroupStatus Status { get; set; } = GroupStatus.Draft;

    public bool IsException { get; set; } = false;

    public string? ExceptionReason { get; set; }

    /// <summary>
    /// Estimated date when group is ready for UAV service
    /// </summary>
    public DateTime? ReadyForUavDate { get; set; }

    [Column(TypeName = "geometry(Polygon,4326)")]
    public Polygon? Area { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal? TotalArea { get; set; }

    // Navigation properties
    [ForeignKey("ClusterId")]
    public Cluster Cluster { get; set; } = null!;

    [ForeignKey("SupervisorId")]
    public Supervisor? Supervisor { get; set; }

    [ForeignKey("RiceVarietyId")]
    public RiceVariety? RiceVariety { get; set; }

    public ICollection<Plot> Plots { get; set; } = new List<Plot>();
    public ICollection<ProductionPlan> ProductionPlans { get; set; } = new List<ProductionPlan>();
    public ICollection<UavServiceOrder> UavServiceOrders { get; set; } = new List<UavServiceOrder>();
    public ICollection<Alert> Alerts { get; set; } = new List<Alert>();
}