using TaskStatus = RiceProduction.Domain.Enums.TaskStatus;

namespace RiceProduction.Domain.Entities;

public class ProductionPlanTask : BaseAuditableEntity
{
    [Required]
    public Guid ProductionPlanId { get; set; }
    
    [Required]
    public Guid StandardPlanTaskId { get; set; }
    
    [Required]
    [MaxLength(255)]
    public string TaskName { get; set; } = string.Empty;
    
    public string? Description { get; set; }
    
    [Required]
    public TaskType TaskType { get; set; }
    
    [Required]
    public DateTime ScheduledDate { get; set; }
    
    public DateTime? ScheduledEndDate { get; set; }
    
    public TaskStatus Status { get; set; } = TaskStatus.Draft;
    
    public TaskPriority Priority { get; set; } = TaskPriority.Normal;
    
    public int SequenceOrder { get; set; }
    
    // Cost tracking
    [Column(TypeName = "decimal(12,2)")]
    public decimal EstimatedMaterialCost { get; set; } = 0;
    
    [Column(TypeName = "decimal(12,2)")]
    public decimal EstimatedServiceCost { get; set; } = 0;
    
    // Navigation properties
    [ForeignKey("ProductionPlanId")]
    public ProductionPlan ProductionPlan { get; set; } = null!;
    
    [ForeignKey("StandardPlanTaskId")]
    public StandardPlanTask StandardPlanTask { get; set; } = null!;
    
    public ICollection<ProductionPlanTaskMaterial> ProductionPlanTaskMaterials { get; set; } = new List<ProductionPlanTaskMaterial>();
    public ICollection<CultivationTask> CultivationTasks { get; set; } = new List<CultivationTask>();
}