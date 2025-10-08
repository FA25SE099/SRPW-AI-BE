namespace RiceProduction.Domain.Entities;

public class StandardPlanTask : BaseAuditableEntity
{
    [Required]
    public Guid StandardStageId { get; set; }

    [Required]
    [MaxLength(255)]
    public string TaskName { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required]
    public int DaysAfter { get; set; }

    public int DurationDays { get; set; } = 1;

    [Required]
    public TaskType TaskType { get; set; }

    public TaskPriority Priority { get; set; } = TaskPriority.Normal;

    [Required]
    public int SequenceOrder { get; set; }

    /// <summary>
    /// The production stage this task belongs to
    /// </summary>
    [Required]
    public Guid StandardProductionStageId { get; set; }


    [ForeignKey("StandardProductionStageId")]
    public StandardPlanStage StandardPlanStage { get; set; } = null!;

    public ICollection<StandardPlanTaskMaterial> StandardPlanTaskMaterials { get; set; } = new List<StandardPlanTaskMaterial>();
}