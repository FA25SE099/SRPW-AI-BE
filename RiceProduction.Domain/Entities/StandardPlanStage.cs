namespace RiceProduction.Domain.Entities;

/// <summary>
/// Defines which production stages are covered by a standard plan template
/// </summary>
public class StandardPlanStage : BaseAuditableEntity
{
    [Required]
    public Guid StandardPlanId { get; set; }


    /// <summary>
    /// Expected duration for this stage in this specific plan
    /// </summary>
    public int? ExpectedDurationDays { get; set; }

    /// <summary>
    /// Order of this stage within this plan
    /// </summary>
    [Required]
    public int SequenceOrder { get; set; }

    /// <summary>
    /// Whether this stage is mandatory for this plan
    /// </summary>
    public bool IsMandatory { get; set; } = true;

    /// <summary>
    /// Additional notes specific to this stage in this plan
    /// </summary>
    [MaxLength(1000)]
    public string? Notes { get; set; }

    // Navigation properties
    [ForeignKey("StandardPlanId")]
    public StandardPlan StandardPlan { get; set; } = null!;
    public ICollection<StandardPlanTask> StandardPlanTasks { get; set; } = new List<StandardPlanTask>();

}

