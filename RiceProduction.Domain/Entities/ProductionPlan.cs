using TaskStatus = RiceProduction.Domain.Enums.TaskStatus;

namespace RiceProduction.Domain.Entities;

public class ProductionPlan : BaseAuditableEntity
{
    public Guid? GroupId { get; set; }

    public Guid? StandardPlanId { get; set; }

    [Required]
    [MaxLength(255)]
    public string PlanName { get; set; } = string.Empty;

    [Required]
    public DateTime BasePlantingDate { get; set; }

    public TaskStatus Status { get; set; } = TaskStatus.Draft;

    [Column(TypeName = "decimal(10,2)")]
    public decimal? TotalArea { get; set; }

    public DateTime? SubmittedAt { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public Guid? ApprovedBy { get; set; }

    public Guid? SubmittedBy { get; set; }

    // Navigation properties
    [ForeignKey("GroupId")]
    public Group? Group { get; set; }

    [ForeignKey("StandardPlanId")]
    public StandardPlan? StandardPlan { get; set; }

    [ForeignKey("ApprovedBy")]
    public AgronomyExpert? Approver { get; set; }

    [ForeignKey("SubmittedBy")]
    public Supervisor? Submitter { get; set; }

    public ICollection<ProductionStage> CurrentProductionStages { get; set; } = new List<ProductionStage>();

    public bool CanBeReviewed => Status == TaskStatus.PendingApproval;

    public bool IsApproved => Status == TaskStatus.Approved;

    public void Submit(Guid submittedBy, DateTime? submittedAt = null)
    {
        var timestamp = submittedAt ?? DateTime.UtcNow;
        Status = TaskStatus.PendingApproval;
        SubmittedBy = submittedBy;
        SubmittedAt = timestamp;
        LastModified = timestamp;
        LastModifiedBy = submittedBy;
    }

    public void Approve(Guid approvedBy, DateTime? approvedAt = null)
    {
        var timestamp = approvedAt ?? DateTime.UtcNow;
        Status = TaskStatus.Approved;
        ApprovedBy = approvedBy;
        ApprovedAt = timestamp;
        LastModified = timestamp;
        LastModifiedBy = approvedBy;
    }

    public void Reject(Guid rejectedBy, DateTime? rejectedAt = null)
    {
        var timestamp = rejectedAt ?? DateTime.UtcNow;
        Status = TaskStatus.Cancelled;
        LastModified = timestamp;
        LastModifiedBy = rejectedBy;
    }
}
