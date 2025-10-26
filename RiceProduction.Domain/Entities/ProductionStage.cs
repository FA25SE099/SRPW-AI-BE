namespace RiceProduction.Domain.Entities;

/// <summary>
/// Represents distinct stages in rice production lifecycle
/// </summary>
public class ProductionStage : BaseAuditableEntity
{
    [Required]
    public Guid ProductionPlanId { get; set; }

    [Required]
    [MaxLength(100)]
    public string StageName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// Sequence order of the stage in production cycle
    /// </summary>
    [Required]
    public int SequenceOrder { get; set; }

    /// <summary>
    /// Typical duration in days for this stage
    /// </summary>
    public int? TypicalDurationDays { get; set; }

    /// <summary>
    /// Color code for UI display (hex color)
    /// </summary>
    [MaxLength(7)]
    public string? ColorCode { get; set; }

    /// <summary>
    /// Whether this stage is active/available for use
    /// </summary>
    public bool IsActive { get; set; } = true;
    [MaxLength(1000)]
    public string? Notes { get; set; }

    // Navigation properties
    public ICollection<ProductionPlanTask> ProductionPlanTasks { get; set; } = new List<ProductionPlanTask>();
    public ProductionPlan ProductionPlan { get; set; } 
}

