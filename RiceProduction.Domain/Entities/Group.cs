namespace RiceProduction.Domain.Entities;
//status: Active, Compleleted, Ready for Optimization
//status 2: có phải là exception không: true false
public class Group : BaseAuditableEntity
{
    [Required]
    public Guid ClusterId { get; set; }

    public Guid? SupervisorId { get; set; }

    public Guid? RiceVarietyId { get; set; }

    public Guid? SeasonId { get; set; }

    public Guid? YearSeasonId { get; set; }
    
    [Required]
    public int Year { get; set; } 

    public DateTime? PlantingDate { get; set; }

    public GroupStatus Status { get; set; } = GroupStatus.Draft;
    public string? GroupName { get; set; }

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

    [ForeignKey("ClusterId")]
    public Cluster Cluster { get; set; } = null!;
    [ForeignKey("SeasonId")]
    public Season? Season { get; set; }
    [ForeignKey("YearSeasonId")]
    public YearSeason? YearSeason { get; set; }
    [ForeignKey("SupervisorId")]
    public Supervisor? Supervisor { get; set; }
    [ForeignKey("RiceVarietyId")]
    public RiceVariety? RiceVariety { get; set; }

    public ICollection<GroupPlot> GroupPlots { get; set; } = new List<GroupPlot>();
    public ICollection<ProductionPlan> ProductionPlans { get; set; } = new List<ProductionPlan>();
    public ICollection<UavServiceOrder> UavServiceOrders { get; set; } = new List<UavServiceOrder>();
    public ICollection<Alert> Alerts { get; set; } = new List<Alert>();
    public ICollection<LateFarmerRecord> LateFarmerRecords { get; set; } = new List<LateFarmerRecord>();
}