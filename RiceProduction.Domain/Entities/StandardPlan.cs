namespace RiceProduction.Domain.Entities;

public class StandardPlan : BaseAuditableEntity
{
    [Required]
    public Guid CategoryId { get; set; }

    [Required]
    public Guid ExpertId { get; set; }

    [Required]
    [MaxLength(255)]
    public string PlanName { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required]
    public int TotalDurationDays { get; set; }

    public Guid? CreatedBy { get; set; }

    public bool IsActive { get; set; } = true;

    [ForeignKey("CategoryId")]
    public RiceVarietyCategory Category { get; set; } = null!;

    [ForeignKey("CreatedBy")]
    public AgronomyExpert? Creator { get; set; }

    public ICollection<ProductionPlan> ProductionPlans { get; set; } = new List<ProductionPlan>();
    public ICollection<StandardPlanStage> StandardPlanStages { get; set; } = new List<StandardPlanStage>();
}