using TaskStatus = RiceProduction.Domain.Enums.TaskStatus;

namespace RiceProduction.Domain.Entities;

public class ProductionPlanTask : BaseAuditableEntity
{
    
    
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

    [Required]
    public Guid ProductionStageId { get; set; }
    
    // Cost tracking
    [Column(TypeName = "decimal(12,2)")]
    public decimal EstimatedMaterialCost { get; set; } = 0;
    
    // Navigation properties
    

    [ForeignKey("ProductionStageId")]
    public ProductionStage ProductionStage { get; set; } = null!;
    
    public ICollection<ProductionPlanTaskMaterial> ProductionPlanTaskMaterials { get; set; } = new List<ProductionPlanTaskMaterial>();
    public ICollection<CultivationTask> CultivationTasks { get; set; } = new List<CultivationTask>();
}