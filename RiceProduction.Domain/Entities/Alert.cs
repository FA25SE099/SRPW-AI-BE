namespace RiceProduction.Domain.Entities;

public class Alert : BaseAuditableEntity
{
    [Required]
    public AlertSource Source { get; set; }

    [Required]
    public AlertSeverity Severity { get; set; }

    public AlertStatus Status { get; set; } = AlertStatus.New;

    // Affected entities
    public Guid? PlotId { get; set; }

    public Guid? GroupId { get; set; }

    public Guid? ClusterId { get; set; }

    // Alert details
    [Required]
    [MaxLength(100)]
    public string AlertType { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;

    // AI data
    [Column(TypeName = "decimal(5,2)")]
    public decimal? AiConfidence { get; set; }

    public bool AiThresholdExceeded { get; set; } = false;

    [Column(TypeName = "jsonb")]
    public string? AiRawData { get; set; }

    // Recommendations
    public string? RecommendedAction { get; set; }

    [Column(TypeName = "jsonb")]
    public string? RecommendedMaterials { get; set; }

    public int? RecommendedUrgencyHours { get; set; }

    // Notifications sent
    public Guid[]? NotifiedUsers { get; set; }

    public DateTime? NotificationSentAt { get; set; }

    // Resolution
    public Guid? AcknowledgedBy { get; set; }

    public DateTime? AcknowledgedAt { get; set; }

    public Guid? ResolvedBy { get; set; }

    public DateTime? ResolvedAt { get; set; }

    public string? ResolutionNotes { get; set; }

    public Guid? CreatedEmergencyTaskId { get; set; }

    // Navigation properties
    [ForeignKey("PlotId")]
    public Plot? Plot { get; set; }

    [ForeignKey("GroupId")]
    public Group? Group { get; set; }

    [ForeignKey("ClusterId")]
    public Cluster? Cluster { get; set; }

    [ForeignKey("AcknowledgedBy")]
    public Supervisor? Acknowledger { get; set; }

    [ForeignKey("ResolvedBy")]
    public Supervisor? Resolver { get; set; }

    [ForeignKey("CreatedEmergencyTaskId")]
    public CultivationTask? CreatedEmergencyTask { get; set; }
}