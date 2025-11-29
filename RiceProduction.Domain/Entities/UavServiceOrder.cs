using TaskStatus = RiceProduction.Domain.Enums.TaskStatus;

namespace RiceProduction.Domain.Entities;
//status: Status: Draft
//trạng thái kiểu khác là bắt đầu khi nào và kết thúc khi nào bởi ai
public class UavServiceOrder : BaseAuditableEntity
{
    [Required]
    public Guid GroupId { get; set; }

    public Guid? UavVendorId { get; set; }

    [Required]
    [MaxLength(255)]
    public string OrderName { get; set; } = string.Empty;

    [Required]
    public DateTime ScheduledDate { get; set; }

    public TimeSpan? ScheduledTime { get; set; }

    public TaskStatus Status { get; set; } = TaskStatus.Draft;

    public TaskPriority Priority { get; set; } = TaskPriority.Normal;

    // Coverage
    [Required]
    [Column(TypeName = "decimal(10,2)")]
    public decimal TotalArea { get; set; }

    [Required]
    public int TotalPlots { get; set; }

    [Column(TypeName = "geometry(LineString,4326)")]
    public LineString? OptimizedRoute { get; set; }

    [Column(TypeName = "jsonb")]
    public string? RouteData { get; set; }

    // Costs
    [Column(TypeName = "decimal(12,2)")]
    public decimal? EstimatedCost { get; set; }

    [Column(TypeName = "decimal(12,2)")]
    public decimal? ActualCost { get; set; }

    // Execution
    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public int CompletionPercentage { get; set; } = 0;

    // Verification
    public Guid? CreatedBy { get; set; }
    public string? Notes { get; set; }

    // Navigation properties
    [ForeignKey("GroupId")]
    public Group Group { get; set; } = null!;

    [ForeignKey("UavVendorId")]
    public UavVendor? UavVendor { get; set; }

    [ForeignKey("CreatedBy")]
    public Supervisor? Creator { get; set; }

    public ICollection<UavInvoice> UavInvoices { get; set; } = new List<UavInvoice>();
}