using TaskStatus = RiceProduction.Domain.Enums.TaskStatus;

namespace RiceProduction.Domain.Entities;

public class CultivationTask : BaseAuditableEntity
{
    public Guid? ProductionPlanTaskId { get; set; }

    [Required]
    public Guid PlotCultivationId { get; set; }

    public Guid? VersionId { get; set; }

    public Guid? AssignedToUserId { get; set; }

    public Guid? AssignedToVendorId { get; set; }
    public string? CultivationTaskName { get; set; }
    public string? Description { get; set; }
    public TaskType? TaskType { get; set; }
    public DateTime? ScheduledEndDate { get; set; }
    public TaskStatus? Status { get; set; }
    /// <summary>
    /// Order for UAV route optimization
    /// </summary>
    public int? ExecutionOrder { get; set; }

    public bool IsContingency { get; set; } = false;

    public string? ContingencyReason { get; set; }

    // Actual execution details
    public DateTime? ActualStartDate { get; set; }
    public DateTime? ActualEndDate { get; set; }

    // Cost tracking
    [Column(TypeName = "decimal(12,2)")]
    public decimal ActualMaterialCost { get; set; } = 0;

    [Column(TypeName = "decimal(12,2)")]
    public decimal ActualServiceCost { get; set; } = 0;


    public DateTime? CompletedAt { get; set; }

    public Guid? VerifiedBy { get; set; }

    public DateTime? VerifiedAt { get; set; }

    public string? WeatherConditions { get; set; }
    public string? InterruptionReason { get; set; }

    // Navigation properties
    [ForeignKey("ProductionPlanTaskId")]
    public ProductionPlanTask? ProductionPlanTask { get; set; } = null!;

    [ForeignKey("PlotCultivationId")]
    public PlotCultivation PlotCultivation { get; set; } = null!;

    [ForeignKey("VersionId")]
    public CultivationVersion? Version { get; set; }

    [ForeignKey("AssignedToUserId")]
    public Supervisor? AssignedSupervisor { get; set; }

    [ForeignKey("AssignedToVendorId")]
    public UavVendor? AssignedVendor { get; set; }

    [ForeignKey("VerifiedBy")]
    public Supervisor? Verifier { get; set; }

    public ICollection<CultivationTaskMaterial> CultivationTaskMaterials { get; set; } = new List<CultivationTaskMaterial>();
    public ICollection<FarmLog> FarmLogs { get; set; } = new List<FarmLog>();
}