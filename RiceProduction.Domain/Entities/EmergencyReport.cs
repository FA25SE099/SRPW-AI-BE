using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Domain.Entities;
//status: theo alert status: Pending, Resolved
public class EmergencyReport : BaseAuditableEntity
{
    [Required]
    public AlertSource Source { get; set; }

    [Required]
    public AlertSeverity Severity { get; set; }

    public AlertStatus Status { get; set; } = AlertStatus.Pending;
    
    public string? Coordinates { get; set; }

    // Affected entities
    public Guid? PlotCultivationId { get; set; }

    public Guid? GroupId { get; set; }

    public Guid? ClusterId { get; set; }

    /// <summary>
    /// The cultivation task where the problem occurred (optional).
    /// Helps identify which stage/task was affected (e.g., "Bón phân lần 2" had pest issue).
    /// </summary>
    public Guid? AffectedCultivationTaskId { get; set; }

        // Alert details
    [Required]
    [MaxLength(100)]
    public string AlertType { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;

    //[Column(TypeName = "jsonb")]
    //public string? AiRawData { get; set; }

    //[Column(TypeName = "jsonb")]
    //public string? RecommendedMaterials { get; set; }

    public int? RecommendedUrgencyHours { get; set; }

    public List<string>? ImageUrls { get; set; }
    
    // AI Pest Detection Results (from pre-analysis via /api/rice/check-pest)
    public bool HasAiAnalysis { get; set; } = false;
    
    public int AiDetectedPestCount { get; set; } = 0;
    
    public List<string>? AiDetectedPestNames { get; set; }
    
    public double? AiAverageConfidence { get; set; }
    
    /// <summary>
    /// Full AI pest detection result stored as JSON
    /// Contains detailed detection data including bounding boxes, confidence levels, etc.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public string? AiPestAnalysisRaw { get; set; }
    
    public DateTime? NotificationSentAt { get; set; }

    public DateTime? NotificationAcknowledgeAt { get; set; }

    public Guid? ResolvedBy { get; set; }
    public Guid? ReportedBy { get; set; }

    public DateTime? ResolvedAt { get; set; }

    public string? ResolutionNotes { get; set; }

    // Navigation properties
    [ForeignKey("PlotCultivationId")]
    public PlotCultivation? PlotCultivation { get; set; }

    [ForeignKey("GroupId")]
    public Group? Group { get; set; }

    [ForeignKey("ClusterId")]
    public Cluster? Cluster { get; set; }

    [ForeignKey("ResolvedBy")]
    public AgronomyExpert? Resolver { get; set; }

    [ForeignKey("ReportedBy")]
    public ApplicationUser? Reporter { get; set; }

    [ForeignKey("AffectedCultivationTaskId")]
    public CultivationTask? AffectedTask { get; set; }

    public List<CultivationTask>? CreatedEmergencyTasks { get; set; }
    
    
}
