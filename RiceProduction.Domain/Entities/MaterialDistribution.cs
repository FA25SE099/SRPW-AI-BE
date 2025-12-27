namespace RiceProduction.Domain.Entities;

/// <summary>
/// Tracks material distribution to farmers when production plan begins
/// Requires confirmation from both supervisor (distributor) and farmer (receiver)
/// </summary>
public class MaterialDistribution : BaseAuditableEntity
{
    [Required]
    public Guid PlotCultivationId { get; set; }
    
    [Required]
    public Guid MaterialId { get; set; }
    
    public Guid? RelatedTaskId { get; set; }
    
    /// <summary>
    /// Quantity of material distributed to this farmer
    /// </summary>
    [Required]
    [Column(TypeName = "decimal(12,2)")]
    public decimal QuantityDistributed { get; set; }
    
    /// <summary>
    /// Status: Pending, PartiallyConfirmed, Completed, Rejected
    /// </summary>
    public DistributionStatus Status { get; set; } = DistributionStatus.Pending;
    
    [Required]
    public DateTime ScheduledDistributionDate { get; set; }
    
    [Required]
    public DateTime DistributionDeadline { get; set; }
    
    public DateTime? ActualDistributionDate { get; set; }
    
    [Required]
    public DateTime SupervisorConfirmationDeadline { get; set; }
    
    public DateTime? FarmerConfirmationDeadline { get; set; }
    
    // Supervisor confirmation
    /// <summary>
    /// Supervisor who confirmed the distribution
    /// </summary>
    public Guid? SupervisorConfirmedBy { get; set; }
    
    /// <summary>
    /// Date when supervisor confirmed distribution
    /// </summary>
    public DateTime? SupervisorConfirmedAt { get; set; }
    
    /// <summary>
    /// Supervisor's notes about the distribution
    /// </summary>
    [MaxLength(500)]
    public string? SupervisorNotes { get; set; }
    
    // Farmer confirmation
    /// <summary>
    /// Date when farmer confirmed receipt
    /// </summary>
    public DateTime? FarmerConfirmedAt { get; set; }
    
    /// <summary>
    /// Farmer's notes about the receipt
    /// </summary>
    [MaxLength(500)]
    public string? FarmerNotes { get; set; }
    
    /// <summary>
    /// Reason if distribution was rejected by either party
    /// </summary>
    [MaxLength(500)]
    public string? RejectionReason { get; set; }
    
    /// <summary>
    /// Image URLs for proof of distribution (photos of materials, delivery, receipt, etc.)
    /// </summary>
    public List<string>? ImageUrls { get; set; }
    
    // Navigation properties
    [ForeignKey("PlotCultivationId")]
    public PlotCultivation PlotCultivation { get; set; } = null!;
    
    [ForeignKey("MaterialId")]
    public Material Material { get; set; } = null!;
    
    [ForeignKey("SupervisorConfirmedBy")]
    public Supervisor? ConfirmedBy { get; set; }
    
    [ForeignKey("RelatedTaskId")]
    public ProductionPlanTask? RelatedTask { get; set; }
}

