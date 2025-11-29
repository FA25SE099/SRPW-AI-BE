using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Domain.Entities;

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

    public List<CultivationTask>? CreatedEmergencyTasks { get; set; }
    
}
