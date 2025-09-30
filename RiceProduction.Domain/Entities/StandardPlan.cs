namespace RiceProduction.Domain.Entities;

public class StandardPlan : BaseAuditableEntity
{
    [Required]
    public Guid RiceVarietyId { get; set; }

    [Required]
    [MaxLength(255)]
    public string PlanName { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required]
    public int TotalDurationDays { get; set; }

    public Guid? CreatedBy { get; set; }

    public bool IsActive { get; set; } = true;

    // Navigation properties
    [ForeignKey("RiceVarietyId")]
    public RiceVariety RiceVariety { get; set; } = null!;

    [ForeignKey("CreatedBy")]
    public AgronomyExpert? Creator { get; set; }

    public ICollection<StandardPlanTask> StandardPlanTasks { get; set; } = new List<StandardPlanTask>();
    public ICollection<ProductionPlan> ProductionPlans { get; set; } = new List<ProductionPlan>();
}