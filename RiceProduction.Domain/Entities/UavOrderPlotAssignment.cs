namespace RiceProduction.Domain.Entities;
public class UavOrderPlotAssignment : BaseAuditableEntity
{
    [Required]
    public Guid UavServiceOrderId { get; set; }

    [Required]
    public Guid PlotId { get; set; }
    
    [Required] 
    public Guid CultivationTaskId { get; set; }
    
    [Required]
    [Column(TypeName = "decimal(10,2)")]
    public decimal ServicedArea { get; set; }
    
    [Column(TypeName = "jsonb")]
    public string? ProofUrlsJson { get; set; } 

    /// <summary>
    /// Trạng thái của dịch vụ trên thửa đất này (Pending, InProgress, Completed).
    /// </summary>
    public RiceProduction.Domain.Enums.TaskStatus Status { get; set; } = RiceProduction.Domain.Enums.TaskStatus.PendingApproval;

    /// <summary>
    /// Chi phí thực tế cho thửa đất này.
    /// </summary>
    [Column(TypeName = "decimal(12,2)")]
    public decimal? ActualCost { get; set; }
    
    public DateTime? CompletionDate { get; set; }
    
    [MaxLength(1000)]
    public string? ReportNotes { get; set; }

    // Navigation properties
    [ForeignKey("UavServiceOrderId")]
    public UavServiceOrder UavServiceOrder { get; set; } = null!;
    
    [ForeignKey("PlotId")]
    public Plot Plot { get; set; } = null!;

    [ForeignKey("CultivationTaskId")]
    public CultivationTask CultivationTask { get; set; } = null!;
}